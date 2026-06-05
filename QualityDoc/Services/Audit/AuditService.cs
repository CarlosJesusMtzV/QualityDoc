using System.Text.Json;
using QualityDoc.Data;
using QualityDoc.Models.Audit;

namespace QualityDoc.Services.Audit;

public class AuditService : IAuditService
{
    private readonly AuditDbContext _db;
    public AuditService(AuditDbContext db) => _db = db;

    public async Task LogAsync(int empresaId, int? usuarioId, string? email, string? rol,
        string accion, string? entidad = null, string? entidadId = null,
        object? detalle = null, string? ip = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            EmpresaId = empresaId,
            UsuarioId = usuarioId,
            UsuarioEmail = email,
            Rol = rol,
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId,
            Detalle = detalle is null ? null : JsonSerializer.Serialize(detalle),
            Ip = ip,
            CreadoEn = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task LogAccesoAsync(int empresaId, int usuarioId, string email,
        int documentoId, int versionId, string versionTag, string accion, string? ip = null)
    {
        _db.AccessLogs.Add(new AccessLog
        {
            EmpresaId = empresaId,
            UsuarioId = usuarioId,
            UsuarioEmail = email,
            DocumentoId = documentoId,
            VersionId = versionId,
            VersionTag = versionTag,
            Accion = accion,
            Ip = ip,
            CreadoEn = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
