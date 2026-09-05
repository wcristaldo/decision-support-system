using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using DecisionSupportAPI.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DecisionSupportAPI.Services;

/// <summary>
/// Exportación del historial de análisis a PDF y Excel/CSV.
/// Gateado por plan (ExportarPdf / ExportarExcel) desde AnalisisController.
/// </summary>
public interface IReporteExportService
{
    byte[] GenerarHistorialPdf(List<AnalisisHistorialDto> historial);
    byte[] GenerarHistorialExcel(List<AnalisisHistorialDto> historial);
    byte[] GenerarHistorialCsv(List<AnalisisHistorialDto> historial);
}

public class ReporteExportService : IReporteExportService
{
    private static readonly CultureInfo EsPy = new("es-PY");

    private static string Recomendacion(string? tipo) => (tipo ?? "").ToUpperInvariant() switch
    {
        "DESPLEGAR"                    => "Desplegar",
        "NO_DESPLEGAR"                  => "No desplegar",
        "DESPLEGAR_CON_OBSERVACIONES"  => "Revisar",
        _                               => tipo ?? "-",
    };

    private static string Pct(decimal? v) => v.HasValue ? $"{v.Value:F1}%" : "-";
    private static string Num(decimal? v) => v.HasValue ? v.Value.ToString("F2", EsPy) : "-";

    // ── PDF ──────────────────────────────────────────────────────────────────
    public byte[] GenerarHistorialPdf(List<AnalisisHistorialDto> historial)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        const string colorNav = "#1c2b3a";
        const string colorSuave = "#5d6d7e";
        const string colorBorde = "#d5d8dc";
        const string colorFondoHead = "#eaf2fb";

        var generado = DateTime.UtcNow.ToLocalTime();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.4f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(8.5f).FontFamily("Arial").FontColor(colorNav));

                page.Header().Column(col =>
                {
                    col.Item().Text("Roshka DSS — Historial de Análisis").FontSize(16).Bold().FontColor(colorNav);
                    col.Item().Text($"Generado el {generado:dd/MM/yyyy HH:mm}").FontSize(8.5f).FontColor(colorSuave);
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(colorBorde);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.2f); // proyecto
                        c.RelativeColumn(1);    // version
                        c.RelativeColumn(1.6f); // fecha
                        c.RelativeColumn(1);    // exito
                        c.RelativeColumn(1);    // cobertura
                        c.RelativeColumn(1);    // fallo
                        c.RelativeColumn(1.1f); // tiempo
                        c.RelativeColumn(1.6f); // recomendacion
                    });

                    table.Header(h =>
                    {
                        foreach (var titulo in new[] { "Proyecto", "Versión", "Fecha", "% Éxito", "Cobertura", "% Fallo", "Tiempo (s)", "Recomendación" })
                            h.Cell().Background(colorFondoHead).Padding(4).Text(titulo).Bold().FontSize(8.5f);
                    });

                    foreach (var r in historial.OrderByDescending(h => h.FechaCarga))
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(colorBorde).Padding(4).Text(r.ProyectoNombre);
                        table.Cell().BorderBottom(0.5f).BorderColor(colorBorde).Padding(4).Text($"v{r.VersionNumero}");
                        table.Cell().BorderBottom(0.5f).BorderColor(colorBorde).Padding(4).Text(r.FechaCarga.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                        table.Cell().BorderBottom(0.5f).BorderColor(colorBorde).Padding(4).Text(Pct(r.TasaExito));
                        table.Cell().BorderBottom(0.5f).BorderColor(colorBorde).Padding(4).Text(Pct(r.Cobertura));
                        table.Cell().BorderBottom(0.5f).BorderColor(colorBorde).Padding(4).Text(Pct(r.TasaFallo));
                        table.Cell().BorderBottom(0.5f).BorderColor(colorBorde).Padding(4).Text(Num(r.TiempoEjecucion));
                        table.Cell().BorderBottom(0.5f).BorderColor(colorBorde).Padding(4).Text(Recomendacion(r.Recomendacion));
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(colorSuave));
                    t.Span("Roshka DSS · Sistema de Apoyo a la Toma de Decisiones · Roshka S.A. · Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();
    }

    // ── Excel (.xlsx) ────────────────────────────────────────────────────────
    public byte[] GenerarHistorialExcel(List<AnalisisHistorialDto> historial)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Historial de análisis");

        string[] headers = ["Proyecto", "Versión", "Fecha", "% Éxito", "Cobertura", "% Fallo", "Tiempo (s)", "Total pruebas", "Exitosas", "Fallidas", "Recomendación"];
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;
        ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#eaf2fb");

        var fila = 2;
        foreach (var r in historial.OrderByDescending(h => h.FechaCarga))
        {
            ws.Cell(fila, 1).Value = r.ProyectoNombre;
            ws.Cell(fila, 2).Value = $"v{r.VersionNumero}";
            ws.Cell(fila, 3).Value = r.FechaCarga.ToLocalTime();
            ws.Cell(fila, 3).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
            ws.Cell(fila, 4).Value = r.TasaExito;
            ws.Cell(fila, 5).Value = r.Cobertura;
            ws.Cell(fila, 6).Value = r.TasaFallo;
            ws.Cell(fila, 7).Value = r.TiempoEjecucion;
            ws.Cell(fila, 8).Value = r.TotalPruebas;
            ws.Cell(fila, 9).Value = r.PruebasExitosas;
            ws.Cell(fila, 10).Value = r.PruebasFallidas;
            ws.Cell(fila, 11).Value = Recomendacion(r.Recomendacion);
            fila++;
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    // ── CSV ──────────────────────────────────────────────────────────────────
    public byte[] GenerarHistorialCsv(List<AnalisisHistorialDto> historial)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Proyecto,Version,Fecha,%Exito,Cobertura,%Fallo,TiempoSegundos,TotalPruebas,Exitosas,Fallidas,Recomendacion");

        static string Csv(string? v)
        {
            v ??= "";
            return v.Contains(',') || v.Contains('"') || v.Contains('\n')
                ? $"\"{v.Replace("\"", "\"\"")}\""
                : v;
        }

        foreach (var r in historial.OrderByDescending(h => h.FechaCarga))
        {
            sb.AppendLine(string.Join(",",
                Csv(r.ProyectoNombre),
                Csv($"v{r.VersionNumero}"),
                Csv(r.FechaCarga.ToLocalTime().ToString("dd/MM/yyyy HH:mm")),
                Csv(Pct(r.TasaExito)),
                Csv(Pct(r.Cobertura)),
                Csv(Pct(r.TasaFallo)),
                Csv(Num(r.TiempoEjecucion)),
                Csv(r.TotalPruebas?.ToString(CultureInfo.InvariantCulture)),
                Csv(r.PruebasExitosas?.ToString(CultureInfo.InvariantCulture)),
                Csv(r.PruebasFallidas?.ToString(CultureInfo.InvariantCulture)),
                Csv(Recomendacion(r.Recomendacion))));
        }

        // BOM UTF-8: Excel abre bien los acentos sin esto se ven mal.
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }
}
