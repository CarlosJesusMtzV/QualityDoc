namespace QualityDoc.Models.Audit;

/// <summary>Estadisticas diarias por empresa (alimentan el dashboard).</summary>
public class DailyStat
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public DateTime Fecha { get; set; }
    public int TotalDocumentos { get; set; }
    public int TotalVersiones { get; set; }
    public int DocsVigentes { get; set; }
    public int DocsEnRevision { get; set; }
    public int DocsRechazados { get; set; }
    public int DocsObsoletos { get; set; }
    public int TotalDescargas { get; set; }
}
