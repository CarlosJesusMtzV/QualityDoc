using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Models.Audit;
using QualityDoc.Models.Domain;
using QualityDoc.Models.ViewModels;
using QualityDoc.Services.Audit;
using QualityDoc.Services.Documents;
using QualityDoc.Services.Search;
using QualityDoc.Services.Storage;
using QualityDoc.Services.Tenant;

namespace QualityDoc.Controllers;

[Authorize]
public class DocumentosController : Controller
{
    private const string PuedeEditar  = "SUPERADMIN,ADMIN,REVISOR,CREADOR";
    private const string PuedeAprobar = "SUPERADMIN,ADMIN,REVISOR";

    private const int PageSize = 10;

    private readonly IDocumentService _docs;
    private readonly IFileStorageService _storage;
    private readonly IAuditService _audit;
    private readonly ITenantContext _tenant;
    private readonly CoreDbContext _db;
    private readonly IConfiguration _config;
    private readonly IMetadataService _meta;

    public DocumentosController(IDocumentService docs, IFileStorageService storage,
        IAuditService audit, ITenantContext tenant, CoreDbContext db, IConfiguration config,
        IMetadataService meta)
    {
        _docs = docs; _storage = storage; _audit = audit; _tenant = tenant;
        _db = db; _config = config; _meta = meta;
    }

    // Lista principal: última versión por documento; el Lector solo ve vigentes.
    // Con texto de búsqueda (q) usa el índice full-text de MongoDB.
    // Pestañas: "vigentes" (solo aprobados vigentes) | "proceso" (borradores y en revisión).
    // Con texto (q) usa el índice full-text de MongoDB.
    public async Task<IActionResult> Index(int? areaId, string? tipo, string? q, string vista = "vigentes", int page = 1)
    {
        if (page < 1) page = 1;
        bool esLector = _tenant.Nivel >= Roles.Nivel[Roles.Lector];
        if (esLector) vista = "vigentes";              // el Lector solo ve vigentes
        if (vista != "proceso") vista = "vigentes";
        bool soloVigentes = vista == "vigentes";

        List<DocumentoVersion> items;
        int total;

        if (!string.IsNullOrWhiteSpace(q))
        {
            var areaNombre = areaId is null ? null
                : await _db.Areas.Where(a => a.Id == areaId).Select(a => a.Nombre).FirstOrDefaultAsync();

            var ids = await _meta.BuscarVersionIdsAsync(_tenant.EmpresaId, q, areaNombre, soloVigentes);
            var encontrados = await _docs.ListarPorIdsAsync(ids, soloVigentes);
            if (!string.IsNullOrWhiteSpace(tipo))
                encontrados = encontrados.Where(v => v.TipoArchivo == tipo).ToList();

            total = encontrados.Count;
            items = encontrados.Skip((page - 1) * PageSize).Take(PageSize).ToList();
        }
        else
        {
            var (it, tot) = await _docs.ListarAsync(areaId, tipo, vista, page, PageSize);
            items = it; total = tot;
        }

        ViewBag.Areas = await AreasSelect(areaId);
        ViewBag.AreaId = areaId;
        ViewBag.Tipo = tipo;
        ViewBag.Q = q;
        ViewBag.Vista = vista;
        ViewBag.EsLector = esLector;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)PageSize);
        ViewBag.Total = total;
        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var doc = await _docs.ObtenerDetalleAsync(id);
        if (doc is null) return NotFound();
        ViewBag.NginxBase = _config["Files:NginxBaseUrl"];
        return View(doc);
    }

    [Authorize(Roles = PuedeEditar)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new DocumentoCreateViewModel();
        await PrepararAreaCreate(vm);
        return View(vm);
    }

    [Authorize(Roles = PuedeEditar)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DocumentoCreateViewModel vm)
    {
        // El Creador (limitado por área) siempre crea en su área.
        if (_tenant.RestringidoPorArea && _tenant.AreaId is not null)
            vm.AreaId = _tenant.AreaId.Value;

        if (vm.Archivo is null || vm.Archivo.Length == 0)
            ModelState.AddModelError(nameof(vm.Archivo), "Sube un archivo válido.");
        if (!ModelState.IsValid)
        {
            await PrepararAreaCreate(vm);
            return View(vm);
        }

        var docId = await _docs.CrearAsync(vm.Titulo, vm.Descripcion, vm.AreaId, vm.Archivo!);
        TempData["Ok"] = "Documento creado en versión v1.0.0 (BORRADOR).";
        return RedirectToAction(nameof(Details), new { id = docId });
    }

    // Si el usuario está limitado por área, el área queda fija (no elige); si no, carga el desplegable.
    private async Task PrepararAreaCreate(DocumentoCreateViewModel vm)
    {
        if (_tenant.RestringidoPorArea && _tenant.AreaId is not null)
        {
            vm.AreaId = _tenant.AreaId.Value;
            ViewBag.AreaFija = await _db.Areas.Where(a => a.Id == _tenant.AreaId).Select(a => a.Nombre).FirstOrDefaultAsync();
        }
        else
        {
            vm.Areas = await AreasSelect(vm.AreaId == 0 ? null : vm.AreaId);
        }
    }

    [Authorize(Roles = PuedeEditar)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var doc = await _docs.ObtenerDetalleAsync(id);
        if (doc is null) return NotFound();
        return View(new DocumentoEditViewModel
        {
            Id = doc.Id,
            Titulo = doc.Titulo,
            Descripcion = doc.Descripcion
        });
    }

    [Authorize(Roles = PuedeEditar)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DocumentoEditViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        await _docs.EditarAsync(vm.Id, vm.Titulo, vm.Descripcion);
        TempData["Ok"] = "Documento actualizado.";
        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    [Authorize(Roles = "SUPERADMIN,ADMIN")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        await _docs.EliminarAsync(id);
        TempData["Ok"] = "Documento dado de baja.";
        return RedirectToAction(nameof(Index));
    }

    // Editar = nueva versión (subir archivo de reemplazo -> nueva edición en BORRADOR).
    [Authorize(Roles = PuedeEditar)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuevaVersion(int documentoId, IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0)
            TempData["Error"] = "Sube un archivo válido para la nueva versión.";
        else
        {
            await _docs.NuevaVersionAsync(documentoId, archivo);
            TempData["Ok"] = "Nueva versión creada (BORRADOR). Envíala a revisión cuando quieras.";
        }
        return RedirectToAction(nameof(Details), new { id = documentoId });
    }

    [Authorize(Roles = PuedeEditar)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubirArchivo(int versionId, int documentoId, IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0) { TempData["Error"] = "Archivo inválido."; }
        else { await _docs.SubirArchivoAsync(versionId, archivo); TempData["Ok"] = "Archivo actualizado."; }
        return RedirectToAction(nameof(Details), new { id = documentoId });
    }

    [Authorize(Roles = PuedeEditar)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarRevision(int versionId, int documentoId)
    {
        try { await _docs.EnviarARevisionAsync(versionId); TempData["Ok"] = "Enviado a revisión."; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id = documentoId });
    }

    [Authorize(Roles = PuedeAprobar)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int versionId, int documentoId, string? comentario)
    {
        try { await _docs.AprobarAsync(versionId, comentario); TempData["Ok"] = "Documento aprobado y vigente."; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id = documentoId });
    }

    [Authorize(Roles = PuedeAprobar)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int versionId, int documentoId, string? comentario)
    {
        try { await _docs.RechazarAsync(versionId, comentario); TempData["Ok"] = "Documento rechazado."; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id = documentoId });
    }

    public async Task<IActionResult> Download(int versionId)
    {
        var v = await _docs.ObtenerVersionAsync(versionId);
        if (v is null || v.RutaArchivo is null) return NotFound();

        // El Lector solo descarga la versión vigente (aprobada).
        if (_tenant.Nivel >= Roles.Nivel[Roles.Lector] && !v.EsVigente)
            return Forbid();

        var file = _storage.Abrir(v.RutaArchivo, v.NombreArchivo ?? "archivo");
        if (file is null) return NotFound();

        await _audit.LogAccesoAsync(_tenant.EmpresaId, _tenant.UsuarioId ?? 0, _tenant.Email ?? "",
            v.DocumentoId, v.Id, v.VersionTag, "DESCARGADO",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        return File(file.Value.Stream, "application/octet-stream", file.Value.Nombre);
    }

    // Vista previa EN LÍNEA (no fuerza descarga). Mismas reglas de acceso que Download.
    public async Task<IActionResult> Ver(int versionId)
    {
        var v = await _docs.ObtenerVersionAsync(versionId);
        if (v is null || v.RutaArchivo is null) return NotFound();

        // El Lector solo puede ver la versión vigente.
        if (_tenant.Nivel >= Roles.Nivel[Roles.Lector] && !v.EsVigente)
            return Forbid();

        var file = _storage.Abrir(v.RutaArchivo, v.NombreArchivo ?? "archivo");
        if (file is null) return NotFound();

        await _audit.LogAccesoAsync(_tenant.EmpresaId, _tenant.UsuarioId ?? 0, _tenant.Email ?? "",
            v.DocumentoId, v.Id, v.VersionTag, "VISUALIZADO",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        // Sin nombre de archivo => Content-Disposition inline => el navegador lo muestra.
        return File(file.Value.Stream, TipoContenido(v.NombreArchivo));
    }

    private static string TipoContenido(string? nombre)
    {
        var ext = System.IO.Path.GetExtension(nombre ?? "").ToLowerInvariant();
        return ext switch
        {
            ".pdf"  => "application/pdf",
            ".png"  => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif"  => "image/gif",
            ".webp" => "image/webp",
            ".svg"  => "image/svg+xml",
            ".txt"  => "text/plain; charset=utf-8",
            _        => "application/octet-stream"
        };
    }

    // Metadatos en vivo: los pide al microservicio Node (que los tiene en MongoDB).
    public async Task<IActionResult> Metadatos(int versionId)
    {
        var v = await _docs.ObtenerVersionAsync(versionId);
        if (v is null) return NotFound();

        var metadatos = new Dictionary<string, string>();
        string? nginxUrl = null;

        var json = await _meta.ObtenerJsonAsync(versionId);
        if (json is not null)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("metadatos", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.Object)
                    foreach (var prop in m.EnumerateObject())
                        metadatos[prop.Name] = prop.Value.ToString();
                if (root.TryGetProperty("nginx_url", out var nu) && nu.ValueKind == System.Text.Json.JsonValueKind.String)
                    nginxUrl = nu.GetString();
            }
            catch { /* JSON inesperado: devolvemos lo básico */ }
        }

        return Json(new
        {
            version = v.VersionTag,
            archivo = v.NombreArchivo,
            tipo = v.TipoArchivo,
            tamanioBytes = v.TamanioBytes,
            estado = v.Estado,
            nginxUrl,
            metadatos
        });
    }

    private async Task<List<SelectListItem>> AreasSelect(int? seleccionada) =>
        await _db.Areas
            .OrderBy(a => a.Nombre)
            .Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = a.Nombre,
                Selected = seleccionada != null && a.Id == seleccionada
            })
            .ToListAsync();
}
