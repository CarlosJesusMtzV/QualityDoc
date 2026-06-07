using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Models.Domain;
using QualityDoc.Models.ViewModels;
using QualityDoc.Services.Audit;
using QualityDoc.Services.Tenant;

namespace QualityDoc.Controllers;

[Authorize(Roles = "SUPERADMIN,ADMIN")]
public class UsuariosController : Controller
{
    private readonly CoreDbContext _db;
    private readonly IAuditService _audit;
    private readonly ITenantContext _tenant;

    public UsuariosController(CoreDbContext db, IAuditService audit, ITenantContext tenant)
    {
        _db = db; _audit = audit; _tenant = tenant;
    }

    public async Task<IActionResult> Index() =>
        View(await _db.Usuarios.Include(u => u.Rol).Include(u => u.Empresa).Include(u => u.Area)
            .OrderBy(u => u.Email).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Create() =>
        View(new UsuarioCreateViewModel { Roles = await RolesSelect(), Areas = await AreasSelect() });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UsuarioCreateViewModel vm)
    {
        if (await _db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == vm.Email))
            ModelState.AddModelError(nameof(vm.Email), "Ya existe un usuario con ese correo.");

        // El rol decide si necesita área (Revisor/Creador/Lector) o no (Admin).
        var rol = await _db.Roles.FirstOrDefaultAsync(r => r.Id == vm.RolId);
        bool requiereArea = rol != null && rol.Nivel >= Roles.Nivel[Roles.Revisor];
        if (requiereArea && (vm.AreaId is null || vm.AreaId == 0))
            ModelState.AddModelError(nameof(vm.AreaId), "Este rol debe pertenecer a un área.");

        if (!ModelState.IsValid) { vm.Roles = await RolesSelect(); vm.Areas = await AreasSelect(); return View(vm); }

        _db.Usuarios.Add(new Usuario
        {
            EmpresaId = _tenant.EmpresaId,
            RolId = vm.RolId,
            AreaId = requiereArea ? vm.AreaId : null,   // Admin no lleva área
            Email = vm.Email,
            Nombre = vm.Nombre,
            Apellido = vm.Apellido,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password, 12)
        });
        await _db.SaveChangesAsync();

        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.UsuarioCreado, "Usuario", vm.Email, new { vm.Email });

        TempData["Ok"] = "Usuario creado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();
        if (u.Id == _tenant.UsuarioId)
        {
            TempData["Error"] = "No puedes desactivar tu propio usuario.";
            return RedirectToAction(nameof(Index));
        }

        u.Activo = !u.Activo;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.UsuarioCreado, "Usuario", u.Id.ToString(), new { activo = u.Activo });

        TempData["Ok"] = u.Activo ? "Usuario activado." : "Usuario desactivado.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> RolesSelect()
    {
        bool esSuper = _tenant.Rol == Roles.SuperAdmin;
        return await _db.Roles
            .Where(r => esSuper || r.Nombre != Roles.SuperAdmin)
            .OrderBy(r => r.Nivel)
            .Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Nombre })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> AreasSelect() =>
        await _db.Areas.OrderBy(a => a.Nombre)
            .Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.Nombre })
            .ToListAsync();
}
