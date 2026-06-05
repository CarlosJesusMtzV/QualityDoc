namespace QualityDoc.Services.Tenant;

/// <summary>Contexto de la empresa/usuario actual, derivado de los claims del login.</summary>
public interface ITenantContext
{
    bool IsAuthenticated { get; }
    int EmpresaId { get; }
    string? EmpresaSlug { get; }
    int? UsuarioId { get; }
    string? Email { get; }
    string? Rol { get; }
    int Nivel { get; }
    /// <summary>True para SuperAdmin o procesos de sistema: omite el filtro por empresa.</summary>
    bool IgnoreTenantFilter { get; }
}
