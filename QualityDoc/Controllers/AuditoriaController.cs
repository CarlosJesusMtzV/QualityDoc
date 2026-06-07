using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Models.Domain;
using QualityDoc.Services.Tenant;

namespace QualityDoc.Controllers;

[Authorize(Roles = "SUPERADMIN,ADMIN")]
public class AuditoriaController : Controller
{
    private readonly AuditDbContext _audit;
    private readonly CoreDbContext _core;
    private readonly ITenantContext _tenant;

    public AuditoriaController(AuditDbContext audit, CoreDbContext core, ITenantContext tenant)
    {
        _audit = audit; _core = core; _tenant = tenant;
    }

    public async Task<IActionResult> Index(string? accion, int? empresaId)
    {
        // global = SuperAdmin en modo "Todas". Admin (o SuperAdmin dentro de una empresa) ve solo la suya.
        bool global = _tenant.IgnoreTenantFilter;

        var q = _audit.AuditLogs.AsQueryable();
        if (!global)
            q = q.Where(l => l.EmpresaId == _tenant.EmpresaId);
        else if (empresaId is not null && empresaId > 0)
            q = q.Where(l => l.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(accion))
            q = q.Where(l => l.Accion == accion);

        var logs = await q.OrderByDescending(l => l.CreadoEn).Take(300).ToListAsync();

        if (global)
        {
            var empresas = await _core.Empresas.IgnoreQueryFilters()
                .Where(e => e.EliminadoEn == null).OrderBy(e => e.Nombre).ToListAsync();
            ViewBag.Empresas = empresas;
            ViewBag.NombrePorEmpresa = empresas.ToDictionary(e => e.Id, e => e.Nombre);
        }

        ViewBag.Global = global;
        ViewBag.EmpresaId = empresaId;
        ViewBag.Accion = accion;
        return View(logs);
    }
}
