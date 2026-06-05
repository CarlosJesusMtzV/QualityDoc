namespace QualityDoc.Models.Domain;

public class Area
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsGlobal { get; set; }
    public DateTime? EliminadoEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public Empresa? Empresa { get; set; }
    public ICollection<Documento> Documentos { get; set; } = new List<Documento>();
}
