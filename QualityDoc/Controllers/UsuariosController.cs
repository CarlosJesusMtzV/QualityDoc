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
        View(await _db.Usuarios.Include(u => u.Rol).Include(u => u.Empresa)
            .OrderBy(u => u.Email).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Create() =>
        View(new UsuarioCreateViewModel { Roles = await RolesSelect() });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UsuarioCreateViewModel vm)
    {
        if (await _db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == vm.Email))
            ModelState.AddModelError(nameof(vm.Email), "Ya existe un usuario con ese correo.");
        if (!ModelState.IsValid) { vm.Roles = await RolesSelect(); return View(vm); }

        _db.Usuarios.Add(new Usuario
        {
            EmpresaId = _tenant.EmpresaId,
            RolId = vm.RolId,
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
}
