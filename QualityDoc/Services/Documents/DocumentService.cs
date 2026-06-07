using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Models.Domain;
using QualityDoc.Services.Audit;
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
    private readonly IMetadataService _meta;   // cliente HTTP hacia Node.js
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;

    public DocumentService(CoreDbContext db, ITenantContext tenant, ISemVerService sem,
        IFileStorageService storage, IMetadataService meta, IAuditService audit, IConfiguration config)
    {
        _db = db; _tenant = tenant; _sem = sem; _storage = storage;
        _meta = meta; _audit = audit; _config = config;
    }

    private string Slug => _tenant.EmpresaSlug ?? "empresa";

    // ── Crear documento + v1.0.0 ──────────────────────────────
    public async Task<int> CrearAsync(string titulo, string? descripcion, int areaId, IFormFile archivo)
    {
        // El Creador (rol limitado por área) solo crea en su propia área.
        if (_tenant.RestringidoPorArea)
        {
            if (_tenant.AreaId is null) throw new InvalidOperationException("Tu usuario no tiene un área asignada.");
            areaId = _tenant.AreaId.Value;
        }

        var doc = new Documento
        {
            EmpresaId = _tenant.EmpresaId,
            AreaId = areaId,
            Titulo = titulo,
            Descripcion = descripcion,
            CreadoPorId = _tenant.UsuarioId ?? 0
        };
        _db.Documentos.Add(doc);
        await _db.SaveChangesAsync();

        var (vm, vn, vp) = _sem.Inicial();   // v1.0.0
        var version = new DocumentoVersion
        {
            DocumentoId = doc.Id,
            EmpresaId = _tenant.EmpresaId,
            VersionMayor = vm, VersionMenor = vn, VersionPatch = vp,
            VersionTag = _sem.Tag(vm, vn, vp),
            Estado = EstadoVersion.Borrador,
            SubidoPorId = _tenant.UsuarioId ?? 0
        };
        _db.DocumentoVersiones.Add(version);
        await _db.SaveChangesAsync();        // obtiene version.Id (carpeta única)

        var areaNombre = await NombreAreaAsync(areaId);
        await GuardarArchivoAsync(version, archivo, NombreBase(titulo, areaNombre));
        await _db.SaveChangesAsync();

        await NotificarIndexAsync(doc, version);
        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.DocCreado, "Documento", doc.Id.ToString(), new { titulo, version = version.VersionTag });
        return doc.Id;
    }

    // ── Editar SOLO datos (título / descripción). El área no se cambia. ──
    public async Task EditarAsync(int documentoId, string titulo, string? descripcion)
    {
        var doc = await _db.Documentos
            .Include(d => d.Area)
            .Include(d => d.Versiones)
            .FirstOrDefaultAsync(d => d.Id == documentoId)
            ?? throw new InvalidOperationException("Documento no encontrado.");

        doc.Titulo = titulo;
        doc.Descripcion = descripcion;
        await _db.SaveChangesAsync();

        foreach (var v in doc.Versiones)
            await _meta.NotificarEstadoAsync(new StatePayload
            {
                DocumentoId = doc.Id, VersionId = v.Id, Estado = v.Estado,
                EsVigente = v.EsVigente, VersionTag = v.VersionTag, Titulo = titulo, Area = doc.Area?.Nombre
            });

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

    // ── Editar = NUEVA versión (reemplazo). Hereda el número del vigente; no cambia aún. ──
    public async Task<int> NuevaVersionAsync(int documentoId, IFormFile archivo)
    {
        var doc = await _db.Documentos
            .Include(d => d.Area)
            .Include(d => d.Versiones)
            .FirstOrDefaultAsync(d => d.Id == documentoId)
            ?? throw new InvalidOperationException("Documento no encontrado.");
        if (_tenant.RestringidoPorArea && doc.AreaId != _tenant.AreaId)
            throw new InvalidOperationException("No tienes acceso a documentos de otra área.");

        var baseV = doc.Versiones.FirstOrDefault(x => x.EsVigente)
                    ?? doc.Versiones.OrderByDescending(x => x.VersionMayor)
                        .ThenByDescending(x => x.VersionMenor).ThenByDescending(x => x.VersionPatch).First();

        var version = new DocumentoVersion
        {
            DocumentoId = doc.Id,
            EmpresaId = doc.EmpresaId,
            VersionMayor = baseV.VersionMayor, VersionMenor = baseV.VersionMenor, VersionPatch = baseV.VersionPatch,
            VersionTag = _sem.Tag(baseV.VersionMayor, baseV.VersionMenor, baseV.VersionPatch),
            Estado = EstadoVersion.Borrador,
            SubidoPorId = _tenant.UsuarioId ?? 0
        };
        _db.DocumentoVersiones.Add(version);
        await _db.SaveChangesAsync();

        await GuardarArchivoAsync(version, archivo, NombreBase(doc.Titulo, doc.Area?.Nombre ?? ""));
        await _db.SaveChangesAsync();

        await NotificarIndexAsync(doc, version);
        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.VersionSubida, "DocumentoVersion", version.Id.ToString(), new { nuevaEdicion = version.VersionTag });
        return version.Id;
    }

    public async Task SubirArchivoAsync(int versionId, IFormFile archivo)
    {
        var v = await CargarVersionAsync(versionId);
        if (v.Estado != EstadoVersion.Borrador)
            throw new InvalidOperationException("Solo se puede cambiar el archivo de una versión en BORRADOR.");

        var areaNombre = v.Documento?.Area?.Nombre ?? await NombreAreaAsync(v.Documento!.AreaId);
        await GuardarArchivoAsync(v, archivo, NombreBase(v.Documento!.Titulo, areaNombre));
        await _db.SaveChangesAsync();

        await NotificarIndexAsync(v.Documento!, v);
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

        v.Estado = EstadoVersion.EnRevision;   // el número NO cambia
        await _db.SaveChangesAsync();

        await NotificarEstadoAsync(v);
        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.DocEnviadoRev, "DocumentoVersion", v.Id.ToString(), new { version = v.VersionTag });
    }

    public async Task AprobarAsync(int versionId, string? comentario)
    {
        var v = await CargarVersionAsync(versionId);
        if (v.Estado != EstadoVersion.EnRevision)
            throw new InvalidOperationException("Solo una versión EN_REVISION puede aprobarse.");

        // ¿Es la primera vez que se aprueba ALGO de este documento?
        bool primera = !await _db.DocumentoVersiones.AnyAsync(x => x.DocumentoId == v.DocumentoId && x.FueAprobada);

        // La versión vigente anterior pasa a OBSOLETO (conserva FueAprobada = true).
        var vigentes = await _db.DocumentoVersiones
            .Where(x => x.DocumentoId == v.DocumentoId && x.EsVigente)
            .ToListAsync();
        foreach (var old in vigentes) { old.EsVigente = false; old.Estado = EstadoVersion.Obsoleto; }
        await _db.SaveChangesAsync();

        var (ma, me, pa) = _sem.AlAprobar(v.VersionMayor, v.VersionMenor, v.VersionPatch, primera);
        v.VersionMayor = ma; v.VersionMenor = me; v.VersionPatch = pa; v.VersionTag = _sem.Tag(ma, me, pa);
        v.Estado = EstadoVersion.Aprobado;
        v.EsVigente = true;
        v.FueAprobada = true;
        v.ComentarioRevision = comentario;
        v.RevisadoPorId = _tenant.UsuarioId;
        v.RevisadoEn = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        foreach (var old in vigentes) await NotificarEstadoAsync(old);
        await NotificarEstadoAsync(v);
        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.DocAprobado, "DocumentoVersion", v.Id.ToString(), new { version = v.VersionTag });
    }

    public async Task RechazarAsync(int versionId, string? comentario)
    {
        var v = await CargarVersionAsync(versionId);
        if (v.Estado != EstadoVersion.EnRevision)
            throw new InvalidOperationException("Solo una versión EN_REVISION puede rechazarse.");

        // Al rechazar sube el 2º número (minor), p.ej. v2.0.0 → v2.1.0.
        // Tomamos el mayor minor existente de ese mismo "major" + 1 para no chocar con otra versión.
        var maxMenor = await _db.DocumentoVersiones
            .Where(x => x.DocumentoId == v.DocumentoId && x.VersionMayor == v.VersionMayor)
            .MaxAsync(x => (int?)x.VersionMenor) ?? 0;
        v.VersionMenor = maxMenor + 1;
        v.VersionPatch = 0;
        v.VersionTag = _sem.Tag(v.VersionMayor, v.VersionMenor, v.VersionPatch);

        // Terminal: queda RECHAZADO, sin crear borrador nuevo.
        v.Estado = EstadoVersion.Rechazado;
        v.ComentarioRevision = comentario;
        v.RevisadoPorId = _tenant.UsuarioId;
        v.RevisadoEn = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await NotificarEstadoAsync(v);
        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.DocRechazado, "DocumentoVersion", v.Id.ToString(), new { rechazada = v.VersionTag });
    }

    // ── Consultas ─────────────────────────────────────────────
    // vista = "vigentes" (solo vigentes) | "proceso" (borradores y en revisión)
    public async Task<(List<DocumentoVersion> Items, int Total)> ListarAsync(
        int? areaId, string? tipo, string vista, int page, int pageSize)
    {
        var q = _db.DocumentoVersiones
            .Include(v => v.Documento)!.ThenInclude(d => d!.Area)
            .AsQueryable();

        if (vista == "proceso")
            q = q.Where(v => v.Estado == EstadoVersion.Borrador || v.Estado == EstadoVersion.EnRevision);
        else
            q = q.Where(v => v.EsVigente);

        // Lector/Creador/Revisor: solo su área.
        if (_tenant.RestringidoPorArea)
            q = q.Where(v => v.Documento!.AreaId == _tenant.AreaId);

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
        if (_tenant.RestringidoPorArea)
            q = q.Where(v => v.Documento!.AreaId == _tenant.AreaId);

        return await q.OrderByDescending(v => v.CreadoEn).ToListAsync();
    }

    public async Task<Documento?> ObtenerDetalleAsync(int documentoId)
    {
        var doc = await _db.Documentos
            .Include(d => d.Area)
            .Include(d => d.CreadoPor)
            .Include(d => d.Versiones.OrderByDescending(v => v.Id))!.ThenInclude(v => v.SubidoPor)
            .FirstOrDefaultAsync(d => d.Id == documentoId);
        if (doc is not null && _tenant.RestringidoPorArea && doc.AreaId != _tenant.AreaId)
            return null;   // otra área: no accesible
        return doc;
    }

    public async Task<DocumentoVersion?> ObtenerVersionAsync(int versionId)
    {
        var v = await _db.DocumentoVersiones
            .Include(v => v.Documento)!.ThenInclude(d => d!.Area)
            .FirstOrDefaultAsync(v => v.Id == versionId);
        if (v is not null && _tenant.RestringidoPorArea && v.Documento!.AreaId != _tenant.AreaId)
            return null;
        return v;
    }

    // ── Helpers ───────────────────────────────────────────────
    private async Task<DocumentoVersion> CargarVersionAsync(int versionId)
    {
        var v = await _db.DocumentoVersiones
            .Include(v => v.Documento)!.ThenInclude(d => d!.Area)
            .FirstOrDefaultAsync(v => v.Id == versionId)
            ?? throw new InvalidOperationException("Versión no encontrada.");
        if (_tenant.RestringidoPorArea && v.Documento!.AreaId != _tenant.AreaId)
            throw new InvalidOperationException("No tienes acceso a documentos de otra área.");
        return v;
    }

    private static string NombreBase(string titulo, string areaNombre) =>
        string.IsNullOrWhiteSpace(areaNombre) ? titulo : $"{titulo} - {areaNombre}";

    // Guarda el archivo en una carpeta única por versión (evita pisar versiones con el mismo número).
    private async Task GuardarArchivoAsync(DocumentoVersion v, IFormFile archivo, string nombreBase)
    {
        var stored = await _storage.GuardarAsync(Slug, v.DocumentoId, v.Id.ToString(), archivo, nombreBase);
        v.NombreArchivo = stored.Nombre;
        v.RutaArchivo = stored.RutaRelativa;
        v.TipoArchivo = stored.Tipo;
        v.HashSha256 = stored.HashSha256;
        v.TamanioBytes = stored.TamanioBytes;
    }

    private async Task<string> NombreAreaAsync(int areaId) =>
        await _db.Areas.Where(a => a.Id == areaId).Select(a => a.Nombre).FirstOrDefaultAsync() ?? string.Empty;

    private async Task NotificarIndexAsync(Documento doc, DocumentoVersion v)
    {
        var areaNombre = doc.Area?.Nombre ?? await NombreAreaAsync(doc.AreaId);
        var nginxBase = _config["Files:NginxBaseUrl"];
        var nginxUrl = string.IsNullOrWhiteSpace(nginxBase) || v.RutaArchivo is null
            ? null
            : $"{nginxBase.TrimEnd('/')}/{v.RutaArchivo}";

        await _meta.NotificarIndexAsync(new IndexPayload
        {
            EmpresaId = v.EmpresaId,
            EmpresaSlug = _tenant.EmpresaSlug,
            DocumentoId = doc.Id,
            VersionId = v.Id,
            VersionTag = v.VersionTag,
            Titulo = doc.Titulo,
            Area = areaNombre,
            TipoArchivo = v.TipoArchivo,
            Estado = v.Estado,
            EsVigente = v.EsVigente,
            RutaArchivo = v.RutaArchivo,
            NombreArchivo = v.NombreArchivo,
            NginxUrl = nginxUrl,
            HashSha256 = v.HashSha256,
            TamanioBytes = v.TamanioBytes,
            SubidoPor = _tenant.Email
        });
    }

    private async Task NotificarEstadoAsync(DocumentoVersion v) =>
        await _meta.NotificarEstadoAsync(new StatePayload
        {
            DocumentoId = v.DocumentoId,
            VersionId = v.Id,
            Estado = v.Estado,
            EsVigente = v.EsVigente,
            VersionTag = v.VersionTag
        });
}
