using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Models.Domain;
using QualityDoc.Models.ViewModels;
using QualityDoc.Services.Audit;
using QualityDoc.Services.Tenant;

namespace QualityDoc.Controllers;

[Authorize(Roles = "SUPERADMIN")]
public class EmpresasController : Controller
{
    private readonly CoreDbContext _db;
    private readonly IAuditService _audit;
    private readonly ITenantContext _tenant;

    public EmpresasController(CoreDbContext db, IAuditService audit, ITenantContext tenant)
    {
        _db = db; _audit = audit; _tenant = tenant;
    }

    public async Task<IActionResult> Index() =>
        View(await _db.Empresas.OrderBy(e => e.Nombre).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View(new EmpresaCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmpresaCreateViewModel vm)
    {
        if (await _db.Empresas.IgnoreQueryFilters().AnyAsync(e => e.Slug == vm.Slug))
            ModelState.AddModelError(nameof(vm.Slug), "Ya existe una empresa con ese slug.");
        if (await _db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == vm.AdminEmail))
            ModelState.AddModelError(nameof(vm.AdminEmail), "Ya existe un usuario con ese correo.");
        if (!ModelState.IsValid) return View(vm);

        var empresa = new Empresa { Nombre = vm.Nombre, Slug = vm.Slug };
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();

        // Áreas por defecto
        _db.Areas.AddRange(
            new Area { EmpresaId = empresa.Id, Nombre = "Calidad", Descripcion = "Sistema de gestión de calidad", EsGlobal = true },
            new Area { EmpresaId = empresa.Id, Nombre = "Seguridad", Descripcion = "Protocolos de seguridad e higiene", EsGlobal = true },
            new Area { EmpresaId = empresa.Id, Nombre = "Recursos Humanos", Descripcion = "Políticas y procedimientos de RRHH", EsGlobal = true },
            new Area { EmpresaId = empresa.Id, Nombre = "Producción", Descripcion = "Manuales y procedimientos de producción", EsGlobal = true });

        // Admin inicial de la empresa
        var rolAdmin = await _db.Roles.FirstAsync(r => r.Nombre == Roles.Admin);
        _db.Usuarios.Add(new Usuario
        {
            EmpresaId = empresa.Id,
            RolId = rolAdmin.Id,
            Email = vm.AdminEmail,
            Nombre = vm.AdminNombre,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.AdminPassword, 12)
        });
        await _db.SaveChangesAsync();

        await _audit.LogAsync(_tenant.EmpresaId, _tenant.UsuarioId, _tenant.Email, _tenant.Rol,
            AccionAudit.EmpresaCreada, "Empresa", empresa.Id.ToString(), new { vm.Nombre, vm.Slug });

        TempData["Ok"] = $"Empresa '{vm.Nombre}' creada con su admin y áreas por defecto.";
        return RedirectToAction(nameof(Index));
    }

    // Fija (o limpia) la "empresa activa" del SuperAdmin. id null/0 = volver a "Todas".
    [HttpGet]
    public async Task<IActionResult> Activar(int? id, string? returnUrl)
    {
        if (id is null || id == 0)
        {
            Response.Cookies.Delete("qd_empresa_activa");
            TempData["Ok"] = "Ahora ves TODAS las empresas (modo global).";
        }
        else
        {
            var emp = await _db.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id);
            if (emp is not null)
            {
                Response.Cookies.Append("qd_empresa_activa", $"{emp.Id}|{emp.Slug}",
                    new CookieOptions { HttpOnly = true, IsEssential = true, Path = "/" });
                TempData["Ok"] = $"Trabajando dentro de: {emp.Nombre}.";
            }
        }
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction("Index", "Dashboard");
    }
}
