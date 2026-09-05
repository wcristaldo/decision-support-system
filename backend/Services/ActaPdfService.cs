using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DecisionSupportAPI.Services;

public record ActaMetricaData(string Nombre, string Valor);

public record ActaData(
    int DecisionId,
    string ProyectoNombre,
    string VersionNumero,
    string? VersionDescripcion,
    string? ArchivoNombre,
    DateTime? ArchivoFecha,
    List<ActaMetricaData> Metricas,
    string RecomendacionTipo,
    string? RecomendacionJustificacion,
    DateTime RecomendacionFecha,
    string DecisionFinal,
    string Comentario,
    bool EsOverride,
    DateTime FechaDecision,
    string UsuarioDecisorNombre
);

public interface IActaPdfService
{
    byte[] GenerarActaPdf(ActaData acta);
}

/// <summary>
/// Genera el "acta de decisión de despliegue": un único documento que consolida
/// la trazabilidad completa de una decisión (RF12/RF13) — proyecto, versión,
/// resultado de pruebas, recomendación automática del sistema y decisión final
/// tomada por el Líder Técnico, incluyendo si contradijo o no la recomendación.
/// </summary>
public class ActaPdfService : IActaPdfService
{
    private const string ColorNav    = "#1c2b3a";
    private const string ColorAzul   = "#2980b9";
    private const string ColorBorde  = "#d5d8dc";
    private const string ColorSuave  = "#5d6d7e";
    private const string ColorBlanco = "#ffffff";
    private const string ColorVerde  = "#1a7a4e";
    private const string ColorRojo   = "#c0392b";
    private const string ColorAmbar  = "#b7770d";
    private const string ColorFondoAmbar = "#fff8e6";

    private static readonly Dictionary<string, string> RecomendacionLabel = new(StringComparer.OrdinalIgnoreCase)
    {
        ["desplegar"] = "Apto para despliegue",
        ["desplegar_con_observaciones"] = "Apto con observaciones (requiere revisión)",
        ["no_desplegar"] = "No apto para despliegue",
    };

    private static readonly Dictionary<string, string> DecisionLabel = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aprobado"] = "Aprobado",
        ["rechazado"] = "Rechazado",
        ["postergado"] = "Postergado",
    };

    private static readonly Dictionary<string, string> DecisionColor = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aprobado"] = ColorVerde,
        ["rechazado"] = ColorRojo,
        ["postergado"] = ColorAmbar,
    };

    public byte[] GenerarActaPdf(ActaData acta)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var esPY = new System.Globalization.CultureInfo("es-PY");
        var fechaDecisionStr = acta.FechaDecision.ToLocalTime().ToString("dd/MM/yyyy HH:mm", esPY);
        var fechaRecomendacionStr = acta.RecomendacionFecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm", esPY);
        var fechaArchivoStr = acta.ArchivoFecha?.ToLocalTime().ToString("dd/MM/yyyy HH:mm", esPY) ?? "—";

        var recomendacionTxt = RecomendacionLabel.GetValueOrDefault(acta.RecomendacionTipo, acta.RecomendacionTipo);
        var decisionTxt = DecisionLabel.GetValueOrDefault(acta.DecisionFinal, acta.DecisionFinal);
        var decisionColor = DecisionColor.GetValueOrDefault(acta.DecisionFinal, ColorNav);

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(2f, Unit.Centimetre);
                page.MarginTop(1.6f, Unit.Centimetre);
                page.MarginBottom(1.4f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Arial").FontColor(ColorNav));

                page.Content().Column(col =>
                {
                    // ── ENCABEZADO ──────────────────────────────────────────
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(58).Height(52)
                           .Border(1f).BorderColor(ColorBorde)
                           .AlignCenter().AlignMiddle()
                           .Text("DSS").FontSize(13).Bold().FontColor(ColorNav);

                        row.ConstantItem(12);

                        row.RelativeItem().AlignCenter().AlignMiddle().Column(c =>
                        {
                            c.Item().Text("ACTA DE DECISIÓN DE DESPLIEGUE")
                             .FontSize(15).Bold().FontColor(ColorNav).AlignCenter();
                            c.Item().PaddingTop(2)
                             .Text("Roshka DSS — Sistema de Apoyo a la Toma de Decisiones")
                             .FontSize(8f).FontColor(ColorSuave).Italic().AlignCenter();
                        });

                        row.ConstantItem(12);

                        row.ConstantItem(150)
                           .Border(1.5f).BorderColor(ColorAzul)
                           .Padding(9).Column(c =>
                           {
                               c.Item().Text("N° de Acta:").FontSize(7.5f).FontColor(ColorSuave);
                               c.Item().PaddingTop(1).Text($"ACTA-{acta.DecisionId:D6}")
                                .FontSize(8.5f).Bold().FontColor(ColorAzul);
                               c.Item().PaddingTop(5).Text($"Fecha: {fechaDecisionStr}")
                                .FontSize(8f).FontColor(ColorNav);
                           });
                    });

                    col.Item().PaddingTop(12).LineHorizontal(1f).LineColor(ColorBorde);
                    col.Item().PaddingTop(10);

                    // ── PROYECTO / VERSIÓN ──────────────────────────────────
                    col.Item().Border(1f).BorderColor(ColorBorde).Column(c =>
                    {
                        c.Item().Background(ColorNav).PaddingHorizontal(10).PaddingVertical(6)
                         .Text("PROYECTO Y VERSIÓN").FontSize(8.5f).Bold().FontColor(ColorBlanco);
                        c.Item().Padding(10).Column(inner =>
                        {
                            inner.Item().Text(t => { t.Span("Proyecto: ").Bold(); t.Span(acta.ProyectoNombre); });
                            inner.Item().PaddingTop(3).Text(t => { t.Span("Versión: ").Bold(); t.Span(acta.VersionNumero); });
                            if (!string.IsNullOrWhiteSpace(acta.VersionDescripcion))
                                inner.Item().PaddingTop(3).Text(t => { t.Span("Descripción: ").Bold(); t.Span(acta.VersionDescripcion); });
                            if (!string.IsNullOrWhiteSpace(acta.ArchivoNombre))
                                inner.Item().PaddingTop(3).Text(t =>
                                {
                                    t.Span("Archivo de resultados: ").Bold();
                                    t.Span($"{acta.ArchivoNombre} (cargado el {fechaArchivoStr})");
                                });
                        });
                    });

                    col.Item().PaddingTop(8);

                    // ── MÉTRICAS ─────────────────────────────────────────────
                    if (acta.Metricas.Count > 0)
                    {
                        col.Item().Border(1f).BorderColor(ColorBorde).Column(c =>
                        {
                            c.Item().Background(ColorNav).PaddingHorizontal(10).PaddingVertical(6)
                             .Text("MÉTRICAS DE CALIDAD EVALUADAS").FontSize(8.5f).Bold().FontColor(ColorBlanco);

                            c.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(3);
                                    cols.RelativeColumn(2);
                                });

                                foreach (var m in acta.Metricas)
                                {
                                    table.Cell().BorderBottom(0.5f).BorderColor(ColorBorde)
                                         .PaddingVertical(6).PaddingHorizontal(10).Text(m.Nombre).FontSize(9f);
                                    table.Cell().BorderBottom(0.5f).BorderColor(ColorBorde)
                                         .PaddingVertical(6).PaddingHorizontal(10).AlignRight()
                                         .Text(m.Valor).FontSize(9f).Bold();
                                }
                            });
                        });

                        col.Item().PaddingTop(8);
                    }

                    // ── RECOMENDACIÓN DEL SISTEMA ──────────────────────────
                    col.Item().Border(1f).BorderColor(ColorBorde).Column(c =>
                    {
                        c.Item().Background(ColorNav).PaddingHorizontal(10).PaddingVertical(6)
                         .Text("RECOMENDACIÓN AUTOMÁTICA DEL SISTEMA").FontSize(8.5f).Bold().FontColor(ColorBlanco);
                        c.Item().Padding(10).Column(inner =>
                        {
                            inner.Item().Text(t => { t.Span("Recomendación: ").Bold(); t.Span(recomendacionTxt); });
                            inner.Item().PaddingTop(3).Text(t => { t.Span("Generada el: ").Bold(); t.Span(fechaRecomendacionStr); });
                            if (!string.IsNullOrWhiteSpace(acta.RecomendacionJustificacion))
                                inner.Item().PaddingTop(3).Text(t =>
                                {
                                    t.Span("Justificación del motor: ").Bold();
                                    t.Span(acta.RecomendacionJustificacion);
                                });
                        });
                    });

                    col.Item().PaddingTop(8);

                    // ── DECISIÓN FINAL ──────────────────────────────────────
                    col.Item().Border(1f).BorderColor(ColorBorde).Column(c =>
                    {
                        c.Item().Background(ColorNav).PaddingHorizontal(10).PaddingVertical(6)
                         .Text("DECISIÓN FINAL DE DESPLIEGUE").FontSize(8.5f).Bold().FontColor(ColorBlanco);
                        c.Item().Padding(10).Column(inner =>
                        {
                            inner.Item().Text(t =>
                            {
                                t.Span("Decisión: ").Bold();
                                t.Span(decisionTxt).FontColor(decisionColor).Bold();
                            });
                            inner.Item().PaddingTop(3).Text(t => { t.Span("Decidido por: ").Bold(); t.Span(acta.UsuarioDecisorNombre); });
                            inner.Item().PaddingTop(3).Text(t => { t.Span("Fecha de la decisión: ").Bold(); t.Span(fechaDecisionStr); });
                            inner.Item().PaddingTop(3).Text(t => { t.Span("Justificación: ").Bold(); t.Span(acta.Comentario); });
                        });
                    });

                    if (acta.EsOverride)
                    {
                        col.Item().PaddingTop(8);
                        col.Item().Background(ColorFondoAmbar).Border(1f).BorderColor(ColorAmbar)
                           .Padding(10).Row(row =>
                           {
                               row.ConstantItem(22).AlignCenter().AlignMiddle()
                                  .Text("[!]").FontSize(9f).FontColor(ColorAmbar).Bold();
                               row.RelativeItem().AlignMiddle()
                                  .Text("Esta decisión CONTRADICE la recomendación automática del sistema. " +
                                        "El Líder Técnico asumió la responsabilidad explícita de apartarse del criterio " +
                                        "sugerido, con la justificación registrada arriba.")
                                  .FontSize(8.5f).FontColor(ColorNav).Bold();
                           });
                    }

                    col.Item().PaddingTop(8);

                    // ── LEYENDA ──────────────────────────────────────────────
                    col.Item().Background("#eaf2fb").Border(1f).BorderColor(ColorAzul)
                       .Padding(10).Row(row =>
                       {
                           row.ConstantItem(22).AlignCenter().AlignMiddle()
                              .Text("[*]").FontSize(9f).FontColor(ColorAzul).Bold();
                           row.RelativeItem().AlignMiddle()
                              .Text("Este documento acredita el registro de una decisión de despliegue dentro de " +
                                    "Roshka DSS. El sistema asiste con una recomendación basada en métricas de calidad; " +
                                    "la responsabilidad de la decisión final es siempre del Líder Técnico que la registra.")
                              .FontSize(8.5f).FontColor(ColorNav).Italic();
                       });
                });

                page.Footer()
                    .BorderTop(0.5f).BorderColor(ColorBorde)
                    .PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem()
                         .Text("Roshka DSS  ·  Roshka S.A.  ·  soporte-dss@roshka.com")
                         .FontSize(7.5f).FontColor("#aaaaaa");
                        r.AutoItem().AlignRight()
                         .Text("Página 1 de 1")
                         .FontSize(7.5f).FontColor("#aaaaaa");
                    });
            });
        });

        return doc.GeneratePdf();
    }
}
