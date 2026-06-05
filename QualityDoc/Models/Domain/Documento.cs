namespace QualityDoc.Models.Domain;

/// <summary>Ficha logica que agrupa todas las versiones de un documento.</summary>
public class Documento
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int AreaId { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int CreadoPorId { get; set; }
    public DateTime? EliminadoEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public Empresa? Empresa { get; set; }
    public Area? Area { get; set; }
    public Usuario? CreadoPor { get; set; }
    public ICollection<DocumentoVersion> Versiones { get; set; } = new List<DocumentoVersion>();
}
