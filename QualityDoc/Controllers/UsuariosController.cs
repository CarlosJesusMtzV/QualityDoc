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

    // Sugiere un correo único a partir del nombre/apellido y el dominio de la empresa.
    [HttpGet]
    public async Task<IActionResult> SugerirCorreo(string? nombre, string? apellido)
    {
        var dominio = Dominio();
        var baseUser = ConstruirUsuario(nombre, apellido);
        if (string.IsNullOrEmpty(baseUser))
            return Json(new { correo = "", dominio });

        var correo = $"{baseUser}@{dominio}";
        int n = 1;
        while (await _db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == correo))
        {
            n++;
            correo = $"{baseUser}{n}@{dominio}";
        }
        return Json(new { correo, dominio });
    }

    // Comprueba en vivo si un correo es válido y está disponible.
    [HttpGet]
    public async Task<IActionResult> CorreoDisponible(string? email)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        bool valido = System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        bool existe = valido && await _db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email);
        return Json(new { valido, disponible = valido && !existe });
    }

    private string Dominio()
    {
        var slug = _tenant.EmpresaSlug;
        return string.IsNullOrWhiteSpace(slug) ? "empresa.com" : $"{slug}.com";
    }

    // "Juan Carlos" + "Pérez García" => "juan.perez"
    private static string ConstruirUsuario(string? nombre, string? apellido)
    {
        static string PrimerToken(string? s)
        {
            s = Ascii((s ?? "").Trim().ToLowerInvariant());
            var first = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return new string(first.Where(char.IsLetterOrDigit).ToArray());
        }
        var n = PrimerToken(nombre);
        var a = PrimerToken(apellido);
        if (n == "" && a == "") return "";
        if (a == "") return n;
        if (n == "") return a;
        return $"{n}.{a}";
    }

    private static string Ascii(string s) => s
        .Replace('á', 'a').Replace('é', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u').Replace('ü', 'u')
        .Replace('ñ', 'n');

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
