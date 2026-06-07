namespace QualityDoc.Models.ViewModels;

/// <summary>Modelo genérico para cualquier reporte (vista imprimible y PDF comparten esto).</summary>
public class ReporteModel
{
    public string Tipo { get; set; } = "";          // inventario | auditoria | kpis | historial
    public string Titulo { get; set; } = "";
    public string Subtitulo { get; set; } = "";      // filtros aplicados
    public string Empresa { get; set; } = "—";
    public DateTime Generado { get; set; } = DateTime.Now;
    public string GeneradoPor { get; set; } = "";

    /// <summary>Tarjetas de resumen/KPI (etiqueta + valor).</summary>
    public List<(string Label, string Valor)> Resumen { get; set; } = new();

    public List<string> Columnas { get; set; } = new();
    public List<string[]> Filas { get; set; } = new();
}
