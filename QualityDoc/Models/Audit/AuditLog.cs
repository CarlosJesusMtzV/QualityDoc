namespace QualityDoc.Models.Audit;

/// <summary>Bitacora de auditoria (PostgreSQL). Logins y movimientos.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public int? UsuarioId { get; set; }
    public string? UsuarioEmail { get; set; }
    public string? Rol { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string? Entidad { get; set; }
    public string? EntidadId { get; set; }
    public string? Detalle { get; set; }   // JSON serializado
    public string? Ip { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
