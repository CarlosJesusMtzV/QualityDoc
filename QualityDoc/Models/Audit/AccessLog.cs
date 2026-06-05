namespace QualityDoc.Models.Audit;

/// <summary>Registro de accesos a documentos (ver / descargar).</summary>
public class AccessLog
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public int UsuarioId { get; set; }
    public string UsuarioEmail { get; set; } = string.Empty;
    public int DocumentoId { get; set; }
    public int VersionId { get; set; }
    public string VersionTag { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty; // VISUALIZADO | DESCARGADO
    public string? Ip { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
