using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Models.Domain;
using QualityDoc.Models.ViewModels;
using QualityDoc.Services.Reports;
using QualityDoc.Services.Tenant;

namespace QualityDoc.Controllers;

[Authorize(Roles = "SUPERADMIN,ADMIN")]
public class ReportesController : Controller
{
    private readonly CoreDbContext _core;
    private readonly AuditDbContext _audit;
    private readonly ITenantContext _tenant;

    public ReportesController(CoreDbContext core, AuditDbContext audit, ITenantContext tenant)
    {
        _core = core; _audit = audit; _tenant = tenant;
    }

    // ── Centro de reportes (menú + filtros) ──
    public async Task<IActionResult> Index()
    {
        ViewBag.Areas = await _core.Areas.OrderBy(a => a.Nombre)
            .Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.Nombre }).ToListAsync();
        ViewBag.Documentos = await _core.Documentos.Include(d => d.Area).OrderBy(d => d.Titulo)
            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Titulo + " (" + d.Area!.Nombre + ")" }).ToListAsync();
        return View();
    }

    // ── 1) Inventario de documentos (SQL Server) ──
    public async Task<IActionResult> Inventario(int? areaId, string? formato)
    {
        bool global = _tenant.IgnoreTenantFilter;
        var docs = await _core.Documentos
            .Include(d => d.Area).Include(d => d.CreadoPor).Include(d => d.Empresa).Include(d => d.Versiones)
            .Where(d => areaId == null || d.AreaId == areaId)
            .OrderBy(d => d.Area!.Nombre).ThenBy(d => d.Titulo)
            .ToListAsync();

        var m = await NuevoReporte("inventario", "Inventario de documentos");
        if (areaId != null)
            m.Subtitulo = "Área: " + (await _core.Areas.Where(a => a.Id == areaId).Select(a => a.Nombre).FirstOrDefaultAsync() ?? "—");

        m.Columnas = global
            ? new() { "Empresa", "Área", "Título", "Estado", "Versión", "Autor", "Creado" }
            : new() { "Área", "Título", "Estado", "Versión", "Autor", "Creado" };

        foreach (var d in docs)
        {
            var v = d.Versiones.FirstOrDefault(x => x.EsVigente)
                    ?? d.Versiones.OrderByDescending(x => x.CreadoEn).FirstOrDefault();
            var fila = new List<string>();
            if (global) fila.Add(d.Empresa?.Nombre ?? "—");
            fila.Add(d.Area?.Nombre ?? "—");
            fila.Add(d.Titulo);
            fila.Add(v?.Estado ?? "—");
            fila.Add(v?.VersionTag ?? "—");
            fila.Add(d.CreadoPor?.NombreCompleto ?? "—");
            fila.Add(d.CreadoEn.ToLocalTime().ToString("yyyy-MM-dd"));
            m.Filas.Add(fila.ToArray());
        }

        m.Resumen.Add(("Documentos", docs.Count.ToString()));
        m.Resumen.Add(("Vigentes", docs.Count(d => d.Versiones.Any(v => v.EsVigente)).ToString()));
        return Salida(m, formato);
    }

    // ── 2) Cumplimiento / Auditoría (PostgreSQL) ──
    public async Task<IActionResult> Auditoria(string? periodo, string? accion, string? formato)
    {
        bool global = _tenant.IgnoreTenantFilter;
        var q = _audit.AuditLogs.AsQueryable();
        if (!global) q = q.Where(l => l.EmpresaId == _tenant.EmpresaId);

        // Periodo por preset (evita el selector de fecha del navegador).
        var ahora = DateTime.UtcNow;
        (DateTime? desde, string etiqueta) = periodo switch
        {
            "7" => (ahora.AddDays(-7), "Últimos 7 días"),
            "30" => (ahora.AddDays(-30), "Últimos 30 días"),
            "90" => (ahora.AddDays(-90), "Últimos 90 días"),
            "mes" => (new DateTime(ahora.Year, ahora.Month, 1), "Este mes"),
            "anio" => (new DateTime(ahora.Year, 1, 1), "Este año"),
            _ => ((DateTime?)null, "Todos los registros")
        };
        if (desde != null) q = q.Where(l => l.CreadoEn >= desde);
        if (!string.IsNullOrWhiteSpace(accion)) q = q.Where(l => l.Accion == accion);

        var logs = await q.OrderByDescending(l => l.CreadoEn).Take(1000).ToListAsync();

        var nombres = global
            ? await _core.Empresas.IgnoreQueryFilters().ToDictionaryAsync(e => e.Id, e => e.Nombre)
            : new Dictionary<int, string>();

        var m = await NuevoReporte("auditoria", "Reporte de cumplimiento / auditoría");
        var filtros = new List<string> { etiqueta };
        if (!string.IsNullOrWhiteSpace(accion)) filtros.Add("acción: " + accion);
        m.Subtitulo = string.Join(" · ", filtros);

        m.Columnas = global
            ? new() { "Fecha", "Empresa", "Usuario", "Rol", "Acción", "Entidad", "Detalle" }
            : new() { "Fecha", "Usuario", "Rol", "Acción", "Entidad", "Detalle" };

        foreach (var l in logs)
        {
            var fila = new List<string> { l.CreadoEn.ToLocalTime().ToString("yyyy-MM-dd HH:mm") };
            if (global) fila.Add(nombres.TryGetValue(l.EmpresaId, out var en) ? en : l.EmpresaId.ToString());
            fila.Add(l.UsuarioEmail ?? "—");
            fila.Add(l.Rol ?? "—");
            fila.Add(l.Accion);
            fila.Add(string.IsNullOrWhiteSpace(l.Entidad) ? "—" : $"{l.Entidad} {l.EntidadId}");
            fila.Add(Resumir(l.Detalle));
            m.Filas.Add(fila.ToArray());
        }

        m.Resumen.Add(("Registros", logs.Count.ToString()));
        m.Resumen.Add(("Aprobaciones", logs.Count(l => l.Accion == AccionAudit.DocAprobado).ToString()));
        m.Resumen.Add(("Rechazos", logs.Count(l => l.Accion == AccionAudit.DocRechazado).ToString()));
        return Salida(m, formato);
    }

    // ── 3) KPIs / estadísticas (SQL Server) ──
    public async Task<IActionResult> Kpis(string? formato)
    {
        var docs = await _core.Documentos.Include(d => d.Area).Include(d => d.Versiones).ToListAsync();
        var versiones = docs.SelectMany(d => d.Versiones).ToList();

        int aprob = versiones.Count(v => v.Estado == EstadoVersion.Aprobado || (v.Estado == EstadoVersion.Obsoleto && v.FueAprobada));
        int rech = versiones.Count(v => v.Estado == EstadoVersion.Rechazado);
        double pctRechazo = (aprob + rech) == 0 ? 0 : Math.Round(100.0 * rech / (aprob + rech), 1);

        var tiempos = versiones.Where(v => v.FueAprobada && v.RevisadoEn != null)
            .Select(v => (v.RevisadoEn!.Value - v.CreadoEn).TotalDays).ToList();
        double promDias = tiempos.Count == 0 ? 0 : Math.Round(tiempos.Average(), 1);

        var m = await NuevoReporte("kpis", "KPIs / indicadores de gestión");
        m.Resumen.Add(("Documentos", docs.Count.ToString()));
        m.Resumen.Add(("Vigentes", docs.Count(d => d.Versiones.Any(v => v.EsVigente)).ToString()));
        m.Resumen.Add(("En proceso", docs.Count(d => d.Versiones.Any(v => v.Estado is EstadoVersion.Borrador or EstadoVersion.EnRevision)
                                                     && !d.Versiones.Any(v => v.EsVigente)).ToString()));
        m.Resumen.Add(("% de rechazo", pctRechazo + "%"));
        m.Resumen.Add(("Aprobación prom.", promDias + " días"));

        m.Columnas = new() { "Área", "Documentos", "Vigentes", "Versiones aprobadas", "Versiones rechazadas" };
        foreach (var g in docs.GroupBy(d => d.Area?.Nombre ?? "—").OrderBy(g => g.Key))
        {
            var vs = g.SelectMany(d => d.Versiones).ToList();
            m.Filas.Add(new[]
            {
                g.Key,
                g.Count().ToString(),
                g.Count(d => d.Versiones.Any(v => v.EsVigente)).ToString(),
                vs.Count(v => v.Estado == EstadoVersion.Aprobado || (v.Estado == EstadoVersion.Obsoleto && v.FueAprobada)).ToString(),
                vs.Count(v => v.Estado == EstadoVersion.Rechazado).ToString()
            });
        }
        return Salida(m, formato);
    }

    // ── 4) Historial de un documento (SQL Server) ──
    public async Task<IActionResult> Historial(int documentoId, string? formato)
    {
        var doc = await _core.Documentos
            .Include(d => d.Area)
            .Include(d => d.Versiones).ThenInclude(v => v.SubidoPor)
            .Include(d => d.Versiones).ThenInclude(v => v.RevisadoPor)
            .FirstOrDefaultAsync(d => d.Id == documentoId);
        if (doc is null) return NotFound();

        var m = await NuevoReporte("historial", "Historial del documento");
        m.Subtitulo = $"{doc.Titulo} · Área: {doc.Area?.Nombre}";
        m.Columnas = new() { "Versión", "Estado", "Vigente", "Tipo", "Subido por", "Fecha", "Revisado por", "Comentario" };

        foreach (var v in doc.Versiones.OrderByDescending(x => x.CreadoEn))
        {
            m.Filas.Add(new[]
            {
                v.VersionTag,
                v.Estado,
                v.EsVigente ? "Sí" : "",
                v.TipoArchivo ?? "—",
                v.SubidoPor?.NombreCompleto ?? "—",
                v.CreadoEn.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                v.RevisadoPor?.NombreCompleto ?? "—",
                v.ComentarioRevision ?? ""
            });
        }

        m.Resumen.Add(("Versiones", doc.Versiones.Count.ToString()));
        m.Resumen.Add(("Aprobadas", doc.Versiones.Count(v => v.Estado == EstadoVersion.Aprobado || (v.Estado == EstadoVersion.Obsoleto && v.FueAprobada)).ToString()));
        m.Resumen.Add(("Rechazadas", doc.Versiones.Count(v => v.Estado == EstadoVersion.Rechazado).ToString()));
        return Salida(m, formato);
    }

    // ── Helpers ──
    private async Task<ReporteModel> NuevoReporte(string tipo, string titulo)
    {
        bool global = _tenant.IgnoreTenantFilter;
        string empresa = global
            ? "Todas las empresas"
            : await _core.Empresas.IgnoreQueryFilters()
                .Where(e => e.Id == _tenant.EmpresaId).Select(e => e.Nombre).FirstOrDefaultAsync() ?? "—";
        return new ReporteModel
        {
            Tipo = tipo, Titulo = titulo, Empresa = empresa,
            Generado = DateTime.Now, GeneradoPor = _tenant.Email ?? ""
        };
    }

    private IActionResult Salida(ReporteModel m, string? formato)
    {
        if (string.Equals(formato, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = ReportePdf.Generar(m);
            var nombre = $"reporte-{m.Tipo}-{DateTime.Now:yyyyMMdd-HHmm}.pdf";
            return File(bytes, "application/pdf", nombre);
        }
        return View("Reporte", m);
    }

    private static string Resumir(string? detalleJson)
    {
        if (string.IsNullOrWhiteSpace(detalleJson)) return "";
        var s = detalleJson.Replace("{", "").Replace("}", "").Replace("\"", "");
        return s.Length > 60 ? s[..60] + "…" : s;
    }
}
