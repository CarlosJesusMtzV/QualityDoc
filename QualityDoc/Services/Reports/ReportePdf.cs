using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QualityDoc.Models.ViewModels;

namespace QualityDoc.Services.Reports;

/// <summary>Convierte un <see cref="ReporteModel"/> en un PDF profesional (QuestPDF).</summary>
public static class ReportePdf
{
    private const string Tinta = "#334155";   // slate-700
    private const string Gris = "#64748b";
    private const string Linea = "#cbd5e1";
    private const string Zebra = "#f1f5f9";

    public static byte[] Generar(ReporteModel r)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Helvetica"));

                // ── Encabezado ──
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("QualityDoc").FontSize(17).Bold().FontColor(Tinta);
                            c.Item().Text(r.Titulo).FontSize(12).SemiBold();
                            if (!string.IsNullOrWhiteSpace(r.Subtitulo))
                                c.Item().Text(r.Subtitulo).FontSize(8).FontColor(Gris);
                        });
                        row.ConstantItem(200).Column(c =>
                        {
                            c.Item().AlignRight().Text($"Empresa: {r.Empresa}").FontSize(8);
                            c.Item().AlignRight().Text($"Generado: {r.Generado:yyyy-MM-dd HH:mm}").FontSize(8);
                            if (!string.IsNullOrWhiteSpace(r.GeneradoPor))
                                c.Item().AlignRight().Text($"Por: {r.GeneradoPor}").FontSize(8).FontColor(Gris);
                        });
                    });
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Linea);
                });

                // ── Contenido ──
                page.Content().PaddingVertical(10).Column(col =>
                {
                    if (r.Resumen.Count > 0)
                    {
                        col.Item().PaddingBottom(10).Row(row =>
                        {
                            for (int i = 0; i < r.Resumen.Count; i++)
                            {
                                var kpi = r.Resumen[i];
                                row.RelativeItem().Border(1).BorderColor("#e2e8f0").Background("#f8fafc").Padding(8).Column(c =>
                                {
                                    c.Item().Text(kpi.Valor).FontSize(15).Bold().FontColor(Tinta);
                                    c.Item().Text(kpi.Label).FontSize(7).FontColor(Gris);
                                });
                                if (i < r.Resumen.Count - 1) row.ConstantItem(8);
                            }
                        });
                    }

                    if (r.Columnas.Count > 0)
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                foreach (var _ in r.Columnas) cols.RelativeColumn();
                            });

                            table.Header(h =>
                            {
                                foreach (var c in r.Columnas)
                                    h.Cell().Background(Tinta).Padding(5).Text(c).FontColor("#ffffff").FontSize(8).SemiBold();
                            });

                            bool alt = false;
                            foreach (var fila in r.Filas)
                            {
                                var bg = alt ? Zebra : "#ffffff";
                                alt = !alt;
                                foreach (var celda in fila)
                                    table.Cell().Background(bg).Padding(4).Text(celda ?? "").FontSize(8);
                            }
                        });

                        if (r.Filas.Count == 0)
                            col.Item().PaddingTop(12).Text("Sin datos para los filtros seleccionados.")
                                .Italic().FontColor(Gris);
                    }
                });

                // ── Pie ──
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("QualityDoc · Gestión Documental de Calidad — pág. ").FontSize(7).FontColor("#94a3b8");
                    t.CurrentPageNumber().FontSize(7).FontColor("#94a3b8");
                    t.Span(" / ").FontSize(7).FontColor("#94a3b8");
                    t.TotalPages().FontSize(7).FontColor("#94a3b8");
                });
            });
        }).GeneratePdf();
    }
}
