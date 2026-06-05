using System.Security.Claims;
using QualityDoc.Models.Domain;

namespace QualityDoc.Services.Tenant;

/// <summary>
/// Lee la empresa/rol del usuario logueado desde los claims.
/// Si no hay request (creacion de esquema/seed al arrancar), no aplica filtro de empresa.
/// </summary>
public class TenantContext : ITenantContext
{
    private readonly ClaimsPrincipal? _user;

    public TenantContext(IHttpContextAccessor accessor)
    {
        _user = accessor.HttpContext?.User;
    }

    public bool IsAuthenticated => _user?.Identity?.IsAuthenticated == true;

    public int EmpresaId => GetInt("empresa_id");
    public string? EmpresaSlug => _user?.FindFirst("empresa_slug")?.Value;
    public int? UsuarioId => IsAuthenticated ? GetInt(ClaimTypes.NameIdentifier) : null;
    public string? Email => _user?.FindFirst(ClaimTypes.Email)?.Value;
    public string? Rol => _user?.FindFirst(ClaimTypes.Role)?.Value;
    public int Nivel => Rol is not null && Roles.Nivel.TryGetValue(Rol, out var n) ? n : 99;

    // SuperAdmin ve todas las empresas; sin sesion (arranque) tampoco se filtra.
    public bool IgnoreTenantFilter => !IsAuthenticated || Rol == Roles.SuperAdmin;

    private int GetInt(string claim)
    {
        var raw = _user?.FindFirst(claim)?.Value;
        return int.TryParse(raw, out var v) ? v : 0;
    }
}
