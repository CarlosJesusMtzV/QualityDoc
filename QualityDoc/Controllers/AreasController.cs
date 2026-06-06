using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Models.Domain;
using QualityDoc.Models.ViewModels;
using QualityDoc.Services.Audit;
using QualityDoc.Services.Tenant;

namespace QualityDoc.Controllers;

[Authorize(Roles = "SUPERADMIN,ADMIN")]
public class AreasController : Controller
{
    private readonly CoreDbContext _db;
    private readonly IAuditService _audit;
    private readonly ITenantContext _tenant;

    public AreasController(CoreDbContext db, IAuditService audit, ITenantContext tenant)
    {
        _db = db; _audit = audit; _tenant = tenant;
    }

    public async Task<IActionResult> Index() =>
        View(await _db.Areas.OrderBy(a => a.Nombre).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View(new AreaCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AreaCreateViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var area = new Area
        {
            EmpresaId = _tenant.EmpresaId,
            Nombre = vm.Nombre,
            Descripcion = vm.Descripcion,
            EsGlobal = false
        };
        _db.Areas.Add(area);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.AreaCreada, "Area", area.Id.ToString(), new { vm.Nombre });

        TempData["Ok"] = "Área creada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        var a = await _db.Areas.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();

        a.EliminadoEn = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.AreaCreada, "Area", a.Id.ToString(), new { baja = true });

        TempData["Ok"] = "Área dada de baja.";
        return RedirectToAction(nameof(Index));
    }
}
