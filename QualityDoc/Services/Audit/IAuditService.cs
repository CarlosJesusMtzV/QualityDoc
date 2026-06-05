namespace QualityDoc.Services.Audit;

/// <summary>Escribe la bitácora de auditoría en PostgreSQL.</summary>
public interface IAuditService
{
    Task LogAsync(int empresaId, int? usuarioId, string? email, string? rol,
        string accion, string? entidad = null, string? entidadId = null,
        object? detalle = null, string? ip = null);

    Task LogAccesoAsync(int empresaId, int usuarioId, string email,
        int documentoId, int versionId, string versionTag, string accion, string? ip = null);
}
