using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Models.Domain;
using QualityDoc.Services.Tenant;

namespace QualityDoc.ViewComponents;

/// <summary>Selector de "empresa activa" — solo se muestra al SuperAdmin.</summary>
public class EmpresaSwitcherViewComponent : ViewComponent
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenant;

    public EmpresaSwitcherViewComponent(CoreDbContext db, ITenantContext tenant)
    {
        _db = db; _tenant = tenant;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (_tenant.Rol != Roles.SuperAdmin) return Content(string.Empty);

        var empresas = await _db.Empresas.IgnoreQueryFilters()
            .Where(e => e.EliminadoEn == null)
            .OrderBy(e => e.Nombre)
            .ToListAsync();

        ViewBag.ActivaId = _tenant.IgnoreTenantFilter ? 0 : _tenant.EmpresaId;
        ViewBag.ReturnUrl = Request.Path + Request.QueryString;
        return View(empresas);
    }
}
