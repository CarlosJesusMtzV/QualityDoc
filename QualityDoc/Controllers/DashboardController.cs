using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Models.Domain;
using QualityDoc.Services.Tenant;

namespace QualityDoc.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenant;

    public DashboardController(CoreDbContext db, ITenantContext tenant)
    {
        _db = db; _tenant = tenant;
    }

    public async Task<IActionResult> Index()
    {
        var versiones = _db.DocumentoVersiones;

        ViewBag.TotalDocumentos = await _db.Documentos.CountAsync();
        ViewBag.Vigentes   = await versiones.CountAsync(v => v.EsVigente);
        ViewBag.EnRevision = await versiones.CountAsync(v => v.Estado == EstadoVersion.EnRevision);
        ViewBag.Borradores = await versiones.CountAsync(v => v.Estado == EstadoVersion.Borrador);
        ViewBag.Rechazados = await versiones.CountAsync(v => v.Estado == EstadoVersion.Rechazado);
        ViewBag.Obsoletos  = await versiones.CountAsync(v => v.Estado == EstadoVersion.Obsoleto);
        ViewBag.Rol = _tenant.Rol;
        ViewBag.EsAdmin = _tenant.Nivel <= Roles.Nivel[Roles.Admin];
        return View();
    }
}
