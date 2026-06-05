using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Models.Domain;
using QualityDoc.Models.Mongo;
using QualityDoc.Services.Audit;
using QualityDoc.Services.Extraction;
using QualityDoc.Services.Search;
using QualityDoc.Services.Storage;
using QualityDoc.Services.Tenant;

namespace QualityDoc.Services.Documents;

public class DocumentService : IDocumentService
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISemVerService _sem;
    private readonly IFileStorageService _storage;
    private readonly IMetadataService _meta;
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;
    private readonly IMetadataExtractor _extractor;

    public DocumentService(CoreDbContext db, ITenantContext tenant, ISemVerService sem,
        IFileStorageService storage, IMetadataService meta, IAuditService audit, IConfiguration config,
        IMetadataExtractor extractor)
    {
        _db = db; _tenant = tenant; _sem = sem; _storage = storage;
        _meta = meta; _audit = audit; _config = config; _extractor = extractor;
    }

    public async Task<int> CrearAsync(string titulo, string? descripcion, int areaId, IFormFile archivo)
    {
        var doc = new Documento
        {
            EmpresaId = _tenant.EmpresaId,
            AreaId = areaId,
            Titulo = titulo,
            Descripcion = descripcion,
            CreadoPorId = _tenant.UsuarioId ?? 0
        };
        _db.Documentos.Add(doc);
        await _db.SaveChangesAsync(); // obtiene doc.Id

        var version = new DocumentoVersion
        {
            DocumentoId = doc.Id,
            EmpresaId = _tenant.EmpresaId,
            VersionMayor = 1, VersionMenor = 0, VersionPatch = 0,
            VersionTag = _sem.Tag(1, 0, 0),
            Estado = EstadoVersion.Borrador,
            SubidoPorId = _tenant.UsuarioId ?? 0
        };
        var stored = await _storage.GuardarAsync(_tenant.EmpresaSlug ?? "empresa", doc.Id, version.VersionTag, archivo);
        AplicarArchivo(version, stored);
        ExtraerMetadatos(version, stored);
        _db.DocumentoVersiones.Add(version);
        await _db.SaveChangesAsync();

        await SincronizarMetaAsync(doc, version, areaId);
        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.DocCreado, "Documento", doc.Id.ToString(), new { titulo, version = version.VersionTag });
        return doc.Id;
    }

    public async Task EditarAsync(int documentoId, string titulo, string? descripcion, int areaId)
    {
        var doc = await _db.Documentos
            .Include(d => d.Versiones)
            .FirstOrDefaultAsync(d => d.Id == documentoId)
            ?? throw new InvalidOperationException("Documento no encontrado.");

        doc.Titulo = titulo;
        doc.Descripcion = descripcion;
        doc.AreaId = areaId;
        doc.Area = null; // fuerza recálculo del nombre de área en la metadata
        await _db.SaveChangesAsync();

        // Re-sincroniza la metadata de la última versión (título/área pueden haber cambiado).
        var latest = doc.Versiones.OrderByDescending(v => v.Id).FirstOrDefault();
        if (latest is not null) await SincronizarMetaAsync(doc, latest, areaId);

        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.DocCreado, "Documento", doc.Id.ToString(), new { editado = true, titulo });
    }

    public async Task EliminarAsync(int documentoId)
    {
        var doc = await _db.Documentos.FirstOrDefaultAsync(d => d.Id == documentoId)
            ?? throw new InvalidOperationException("Documento no encontrado.");

        doc.EliminadoEn = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.DocCreado, "Documento", doc.Id.ToString(), new { baja = true });
    }

    public async Task SubirArchivoAsync(int versionId, IFormFile archivo)
    {
        var v = await CargarVersionAsync(versionId);
        if (v.Estado != EstadoVersion.Borrador)
            throw new InvalidOperationException("Solo se puede cambiar el archivo de una versión en BORRADOR.");

        var stored = await _storage.GuardarAsync(_tenant.EmpresaSlug ?? "empresa", v.DocumentoId, v.VersionTag, archivo);
        AplicarArchivo(v, stored);
        ExtraerMetadatos(v, stored);
        await _db.SaveChangesAsync();

        await SincronizarMetaAsync(v.Documento!, v, v.Documento!.AreaId);
        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.VersionSubida, "DocumentoVersion", v.Id.ToString(), new { version = v.VersionTag });
    }

    public async Task EnviarARevisionAsync(int versionId)
    {
        var v = await CargarVersionAsync(versionId);
        if (v.Estado != EstadoVersion.Borrador)
            throw new InvalidOperationException("Solo una versión en BORRADOR puede enviarse a revisión.");
        if (v.RutaArchivo is null)
            throw new InvalidOperationException("Sube un archivo antes de enviar a revisión.");

        v.Estado = EstadoVersion.EnRevision;
        await _db.SaveChangesAsync();

        await SincronizarMetaAsync(v.Documento!, v, v.Documento!.AreaId);
        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.DocEnviadoRev, "DocumentoVersion", v.Id.ToString(), new { version = v.VersionTag });
    }

    public async Task AprobarAsync(int versionId, string? comentario)
    {
        var v = await CargarVersionAsync(versionId);
        if (v.Estado != EstadoVersion.EnRevision)
            throw new InvalidOperationException("Solo una versión EN_REVISION puede aprobarse.");

        // La versión vigente anterior pasa a OBSOLETO (conserva FueAprobada = true).
        var vigentes = await _db.DocumentoVersiones
            .Where(x => x.DocumentoId == v.DocumentoId && x.EsVigente)
            .ToListAsync();
        foreach (var old in vigentes) { old.EsVigente = false; old.Estado = EstadoVersion.Obsoleto; }
        await _db.SaveChangesAsync(); // libera el índice único de "una vigente"

        var (ma, me, pa) = _sem.Siguiente(v.VersionMayor, v.VersionMenor, v.VersionPatch, null, esAprobacion: true);
        v.VersionMayor = ma; v.VersionMenor = me; v.VersionPatch = pa; v.VersionTag = _sem.Tag(ma, me, pa);
        v.Estado = EstadoVersion.Aprobado;
        v.EsVigente = true;
        v.FueAprobada = true;
        v.ComentarioRevision = comentario;
        v.RevisadoPorId = _tenant.UsuarioId;
        v.RevisadoEn = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await SincronizarMetaAsync(v.Documento!, v, v.Documento!.AreaId);
        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.DocAprobado, "DocumentoVersion", v.Id.ToString(), new { version = v.VersionTag });
    }

    public async Task RechazarAsync(int versionId, string tipoRechazo, string? comentario)
    {
        var v = await CargarVersionAsync(versionId);
        if (v.Estado != EstadoVersion.EnRevision)
            throw new InvalidOperationException("Solo una versión EN_REVISION puede rechazarse.");

        v.Estado = EstadoVersion.Rechazado;
        v.TipoRechazo = tipoRechazo;
        v.ComentarioRevision = comentario;
        v.RevisadoPorId = _tenant.UsuarioId;
        v.RevisadoEn = DateTime.UtcNow;

        // Nueva versión BORRADOR con el número que corresponde, partiendo del archivo rechazado.
        var (ma, me, pa) = _sem.Siguiente(v.VersionMayor, v.VersionMenor, v.VersionPatch, tipoRechazo, esAprobacion: false);
        var nueva = new DocumentoVersion
        {
            DocumentoId = v.DocumentoId,
            EmpresaId = v.EmpresaId,
            VersionMayor = ma, VersionMenor = me, VersionPatch = pa,
            VersionTag = _sem.Tag(ma, me, pa),
            Estado = EstadoVersion.Borrador,
            SubidoPorId = _tenant.UsuarioId ?? 0,
            TipoArchivo = v.TipoArchivo,
            NombreArchivo = v.NombreArchivo,
            RutaArchivo = v.RutaArchivo,
            HashSha256 = v.HashSha256,
            TamanioBytes = v.TamanioBytes
        };
        _db.DocumentoVersiones.Add(nueva);
        await _db.SaveChangesAsync();

        await SincronizarMetaAsync(v.Documento!, v, v.Documento!.AreaId);
        await SincronizarMetaAsync(v.Documento!, nueva, v.Documento!.AreaId);
        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.DocRechazado, "DocumentoVersion", v.Id.ToString(),
            new { rechazada = v.VersionTag, tipo = tipoRechazo, nueva = nueva.VersionTag });
    }

    public async Task<(List<DocumentoVersion> Items, int Total)> ListarAsync(
        int? areaId, string? tipo, bool soloVigentes, int page, int pageSize)
    {
        var q = _db.DocumentoVersiones
            .Include(v => v.Documento)!.ThenInclude(d => d!.Area)
            .AsQueryable();

        if (soloVigentes)
            q = q.Where(v => v.EsVigente);
        else
            // última versión por documento (Id máximo)
            q = q.Where(v => v.Id == _db.DocumentoVersiones
                .Where(x => x.DocumentoId == v.DocumentoId).Max(x => x.Id));

        if (areaId is not null) q = q.Where(v => v.Documento!.AreaId == areaId);
        if (!string.IsNullOrWhiteSpace(tipo)) q = q.Where(v => v.TipoArchivo == tipo);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(v => v.CreadoEn)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<List<DocumentoVersion>> ListarPorIdsAsync(IEnumerable<int> versionIds, bool soloVigentes)
    {
        var ids = versionIds.ToList();
        if (ids.Count == 0) return new List<DocumentoVersion>();

        var q = _db.DocumentoVersiones
            .Include(v => v.Documento)!.ThenInclude(d => d!.Area)
            .Where(v => ids.Contains(v.Id));
        if (soloVigentes) q = q.Where(v => v.EsVigente);

        return await q.OrderByDescending(v => v.CreadoEn).ToListAsync();
    }

    public async Task<Documento?> ObtenerDetalleAsync(int documentoId) =>
        await _db.Documentos
            .Include(d => d.Area)
            .Include(d => d.CreadoPor)
            .Include(d => d.Versiones.OrderByDescending(v => v.Id))!.ThenInclude(v => v.SubidoPor)
            .FirstOrDefaultAsync(d => d.Id == documentoId);

    public async Task<DocumentoVersion?> ObtenerVersionAsync(int versionId) =>
        await _db.DocumentoVersiones
            .Include(v => v.Documento)!.ThenInclude(d => d!.Area)
            .FirstOrDefaultAsync(v => v.Id == versionId);

    // ── helpers ───────────────────────────────────────────────
    private async Task<DocumentoVersion> CargarVersionAsync(int versionId) =>
        await _db.DocumentoVersiones
            .Include(v => v.Documento)!.ThenInclude(d => d!.Area)
            .FirstOrDefaultAsync(v => v.Id == versionId)
        ?? throw new InvalidOperationException("Versión no encontrada.");

    private static void AplicarArchivo(DocumentoVersion v, StoredFile f)
    {
        v.NombreArchivo = f.Nombre;
        v.RutaArchivo = f.RutaRelativa;
        v.TipoArchivo = f.Tipo;
        v.HashSha256 = f.HashSha256;
        v.TamanioBytes = f.TamanioBytes;
    }

    // Extrae los metadatos del archivo recién guardado y los deja en la versión.
    private void ExtraerMetadatos(DocumentoVersion v, StoredFile stored)
    {
        var f = _storage.Abrir(stored.RutaRelativa, stored.Nombre);
        if (f is null) return;
        using var stream = f.Value.Stream;
        var extr = _extractor.Extraer(stream, stored.Nombre, stored.Tipo);
        v.MetadatosJson = JsonSerializer.Serialize(extr.Propiedades);
        v.TextoExtracto = extr.TextoBusqueda;
    }

    private async Task SincronizarMetaAsync(Documento doc, DocumentoVersion v, int areaId)
    {
        var areaNombre = doc.Area?.Nombre
            ?? (await _db.Areas.Where(a => a.Id == areaId).Select(a => a.Nombre).FirstOrDefaultAsync())
            ?? string.Empty;

        // URL pública en Nginx (si está configurado el file server).
        var nginxBase = _config["Files:NginxBaseUrl"];
        var nginxUrl = string.IsNullOrWhiteSpace(nginxBase) || v.RutaArchivo is null
            ? null
            : $"{nginxBase.TrimEnd('/')}/{v.RutaArchivo}";

        // Metadatos extraídos (se guardan como objeto JSON en Mongo) + etiquetas.
        var metadatos = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(v.MetadatosJson))
        {
            try { metadatos = JsonSerializer.Deserialize<Dictionary<string, string>>(v.MetadatosJson) ?? new(); }
            catch { /* ignore */ }
        }
        var etiquetas = new List<string>();
        if (metadatos.TryGetValue("PalabrasClave", out var kw) && !string.IsNullOrWhiteSpace(kw))
            etiquetas = kw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        await _meta.UpsertAsync(new DocumentMetadata
        {
            NginxUrl = nginxUrl,
            Metadatos = metadatos,
            Etiquetas = etiquetas,
            TextoExtracto = v.TextoExtracto,
            EmpresaId = v.EmpresaId,
            EmpresaSlug = _tenant.EmpresaSlug ?? string.Empty,
            DocumentoId = doc.Id,
            VersionId = v.Id,
            VersionTag = v.VersionTag,
            Titulo = doc.Titulo,
            Area = areaNombre,
            TipoArchivo = v.TipoArchivo,
            Estado = v.Estado,
            EsVigente = v.EsVigente,
            RutaArchivo = v.RutaArchivo,
            HashSha256 = v.HashSha256,
            TamanioBytes = v.TamanioBytes,
            SubidoPor = _tenant.Email,
            FechaSubida = v.CreadoEn
        });
    }
}
