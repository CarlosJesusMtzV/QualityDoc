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
    /// <summary>Área del usuario (Lector/Creador/Revisor). Null para Admin/SuperAdmin.</summary>
    int? AreaId { get; }
    /// <summary>True si el rol está limitado a su área (Revisor, Creador, Lector).</summary>
    bool RestringidoPorArea { get; }
    /// <summary>True para SuperAdmin o procesos de sistema: omite el filtro por empresa.</summary>
    bool IgnoreTenantFilter { get; }
}
