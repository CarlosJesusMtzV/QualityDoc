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
    private readonly ITenantContext _tenant;

    public AuditoriaController(AuditDbContext audit, ITenantContext tenant)
    {
        _audit = audit; _tenant = tenant;
    }

    public async Task<IActionResult> Index(string? accion)
    {
        var q = _audit.AuditLogs.AsQueryable();

        // El admin solo ve su empresa; el superadmin ve todo.
        if (_tenant.Rol != Roles.SuperAdmin)
            q = q.Where(l => l.EmpresaId == _tenant.EmpresaId);

        if (!string.IsNullOrWhiteSpace(accion))
            q = q.Where(l => l.Accion == accion);

        var logs = await q.OrderByDescending(l => l.CreadoEn).Take(300).ToListAsync();
        ViewBag.Accion = accion;
        return View(logs);
    }
}
