using System.Security.Claims;
using QualityDoc.Models.Domain;

namespace QualityDoc.Services.Tenant;

/// <summary>
/// Empresa/rol del usuario logueado (desde los claims).
/// El SuperAdmin puede fijar una "empresa activa" (cookie qd_empresa_activa = "id|slug"):
///   - sin empresa activa  → ve TODO (IgnoreTenantFilter) en modo global.
///   - con empresa activa  → actúa como esa empresa (crea/edita/revisa dentro de ella).
/// </summary>
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;

    public TenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
    public string? Rol => User?.FindFirst(ClaimTypes.Role)?.Value;
    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value;
    public int? UsuarioId => IsAuthenticated ? GetInt(ClaimTypes.NameIdentifier) : null;
    public int Nivel => Rol is not null && Roles.Nivel.TryGetValue(Rol, out var n) ? n : 99;

    public int? AreaId
    {
        get { var raw = User?.FindFirst("area_id")?.Value; return int.TryParse(raw, out var v) && v > 0 ? v : null; }
    }

    // Autorizador(2), Revisor(3), Creador(4), Lector(5) quedan limitados a su área. Admin(1)/SuperAdmin(0) no.
    public bool RestringidoPorArea => Nivel >= Roles.Nivel[Roles.Autorizador];

    public int EmpresaId => SuperOverride()?.Id ?? GetInt("empresa_id");
    public string? EmpresaSlug => SuperOverride()?.Slug ?? User?.FindFirst("empresa_slug")?.Value;

    // El SuperAdmin ve todo SOLO cuando no tiene una empresa activa seleccionada.
    public bool IgnoreTenantFilter =>
        !IsAuthenticated || (Rol == Roles.SuperAdmin && SuperOverride() is null);

    private (int Id, string Slug)? SuperOverride()
    {
        if (Rol != Roles.SuperAdmin) return null;
        var raw = _accessor.HttpContext?.Request.Cookies["qd_empresa_activa"];
        if (string.IsNullOrEmpty(raw)) return null;
        var parts = raw.Split('|', 2);
        return parts.Length == 2 && int.TryParse(parts[0], out var id) && id > 0
            ? (id, parts[1])
            : null;
    }

    private int GetInt(string claim)
    {
        var raw = User?.FindFirst(claim)?.Value;
        return int.TryParse(raw, out var v) ? v : 0;
    }
}
