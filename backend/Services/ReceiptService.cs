using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DecisionSupportAPI.Services;

public interface IReceiptService
{
    byte[] GenerarReciboPdf(ReciboData recibo);
}

public class ReceiptService : IReceiptService
{
    // ── Datos del emisor ──────────────────────────────────────────────────────
    private const string EmisorNombre   = "Roshka DSS";
    private const string EmisorEmpresa  = "Roshka S.A.";
    private const string EmisorRuc      = "80012345-6";
    private const string EmisorDir      = "Aviadores del Chaco 1669, Asunción, Paraguay";
    private const string EmisorEmail    = "soporte@roshka.com";
    private const string SistemaLeyenda = "Sistema Roshka DSS";

    // ── Paleta ────────────────────────────────────────────────────────────────
    private const string ColorNav    = "#1c2b3a";   // navy — encabezados de sección
    private const string ColorAzul   = "#2980b9";   // azul — borde N° recibo
    private const string ColorBorde  = "#d5d8dc";
    private const string ColorFondoS = "#eaf2fb";   // fondo caja leyenda
    private const string ColorVerde  = "#27ae60";   // estado Emitido
    private const string ColorSuave  = "#5d6d7e";   // texto secundario
    private const string ColorBlanco = "#ffffff";

    public byte[] GenerarReciboPdf(ReciboData recibo)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var esPY = new System.Globalization.CultureInfo("es-PY");

        // ── Fecha de emisión (usa server time) ────────────────────────────────
        var emision = (recibo.FechaEmision ?? recibo.FechaPago).ToLocalTime();
        var fechaEmisionStr = emision.ToString("dd/MM/yyyy", esPY);
        var horaEmisionStr  = emision.ToString("HH:mm:ss",  esPY);
        var fechaTraza      = emision.ToString("dd/MM/yyyy HH:mm:ss", esPY);

        // ── Período ───────────────────────────────────────────────────────────
        var periodoStr = (recibo.FechaInicio.HasValue && recibo.FechaVencimiento.HasValue)
            ? $"{recibo.FechaInicio.Value.ToLocalTime():dd/MM/yyyy} – {recibo.FechaVencimiento.Value.ToLocalTime():dd/MM/yyyy}"
            : "—";

        // ── Montos ────────────────────────────────────────────────────────────
        var montoFmt     = $"Gs. {recibo.Monto.ToString("N0", esPY)}";
        var importeLetra = Capitalizar(NumeroALetras(recibo.Monto));

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
                    // ─────────────────────────────────────────────────────────
                    // 1. ENCABEZADO
                    // ─────────────────────────────────────────────────────────
                    col.Item().Row(row =>
                    {
                        // Placeholder logo
                        row.ConstantItem(58).Height(52)
                           .Border(1f).BorderColor(ColorBorde)
                           .AlignCenter().AlignMiddle()
                           .Text("DSS").FontSize(13).Bold().FontColor(ColorNav);

                        row.ConstantItem(12);

                        // Título centrado
                        row.RelativeItem().AlignCenter().AlignMiddle().Column(c =>
                        {
                            c.Item().Text("RECIBO ELECTRÓNICO")
                             .FontSize(17).Bold().FontColor(ColorNav).AlignCenter();
                            c.Item().PaddingTop(2)
                             .Text("Documento no válido como factura")
                             .FontSize(8f).FontColor(ColorSuave).Italic().AlignCenter();
                        });

                        row.ConstantItem(12);

                        // Caja identificación
                        row.ConstantItem(168)
                           .Border(1.5f).BorderColor(ColorAzul)
                           .Padding(9).Column(c =>
                           {
                               c.Item().Text("N° de Recibo:").FontSize(7.5f).FontColor(ColorSuave);
                               c.Item().PaddingTop(1).Text(recibo.NumeroRecibo)
                                .FontSize(8.5f).Bold().FontColor(ColorAzul);
                               c.Item().PaddingTop(5).Text($"Fecha de Emisión: {fechaEmisionStr}")
                                .FontSize(8f).FontColor(ColorNav);
                               c.Item().Text($"Hora de Emisión:  {horaEmisionStr}")
                                .FontSize(8f).FontColor(ColorNav);
                               c.Item().PaddingTop(5).Row(r =>
                               {
                                   r.AutoItem().Text("Estado: ").FontSize(8f);
                                   r.AutoItem().Text(recibo.EstadoRecibo)
                                    .FontSize(8f).Bold().FontColor(ColorVerde);
                               });
                           });
                    });

                    col.Item().PaddingTop(12).LineHorizontal(1f).LineColor(ColorBorde);
                    col.Item().PaddingTop(10);

                    // ─────────────────────────────────────────────────────────
                    // 2. EMISOR / PAGADOR
                    // ─────────────────────────────────────────────────────────
                    col.Item().Row(row =>
                    {
                        // Emisor
                        row.RelativeItem().Border(1f).BorderColor(ColorBorde).Column(c =>
                        {
                            c.Item().Background(ColorNav).PaddingHorizontal(10).PaddingVertical(6)
                             .Text("EMISOR").FontSize(8.5f).Bold().FontColor(ColorBlanco);
                            c.Item().Padding(10).Column(inner =>
                            {
                                inner.Item().Text(t => { t.Span("Nombre: ").Bold(); t.Span(EmisorEmpresa); });
                                inner.Item().PaddingTop(3).Text(t => { t.Span("RUC: ").Bold(); t.Span(EmisorRuc); });
                                inner.Item().PaddingTop(3).Text(t => { t.Span("Correo electrónico: ").Bold(); t.Span(EmisorEmail); });
                                inner.Item().PaddingTop(3).Text(t => { t.Span("Dirección: ").Bold(); t.Span(EmisorDir); });
                            });
                        });

                        row.ConstantItem(8);

                        // Pagador
                        row.RelativeItem().Border(1f).BorderColor(ColorBorde).Column(c =>
                        {
                            c.Item().Background(ColorNav).PaddingHorizontal(10).PaddingVertical(6)
                             .Text("PAGADOR").FontSize(8.5f).Bold().FontColor(ColorBlanco);
                            c.Item().Padding(10).Column(inner =>
                            {
                                inner.Item().Text(t =>
                                {
                                    t.Span("Nombre / Razón Social: ").Bold();
                                    t.Span(recibo.NombreCliente ?? "—");
                                });
                                if (!string.IsNullOrWhiteSpace(recibo.DocumentoCliente))
                                    inner.Item().PaddingTop(3).Text(t =>
                                    {
                                        t.Span("Documento / RUC: ").Bold();
                                        t.Span(recibo.DocumentoCliente);
                                    });
                                if (!string.IsNullOrWhiteSpace(recibo.DireccionCliente))
                                    inner.Item().PaddingTop(3).Text(t =>
                                    {
                                        t.Span("Dirección: ").Bold();
                                        t.Span(recibo.DireccionCliente);
                                    });
                                if (!string.IsNullOrWhiteSpace(recibo.EmailCliente))
                                    inner.Item().PaddingTop(3).Text(t =>
                                    {
                                        t.Span("Correo electrónico: ").Bold();
                                        t.Span(recibo.EmailCliente);
                                    });
                                if (!string.IsNullOrWhiteSpace(recibo.TelefonoCliente))
                                    inner.Item().PaddingTop(3).Text(t =>
                                    {
                                        t.Span("Teléfono: ").Bold();
                                        t.Span(recibo.TelefonoCliente);
                                    });
                            });
                        });
                    });

                    col.Item().PaddingTop(8);

                    // ─────────────────────────────────────────────────────────
                    // 3. DETALLE DEL CONCEPTO
                    // ─────────────────────────────────────────────────────────
                    col.Item().Border(1f).BorderColor(ColorBorde).Column(c =>
                    {
                        c.Item().Background(ColorNav).PaddingHorizontal(10).PaddingVertical(6)
                         .Text("DETALLE DEL CONCEPTO").FontSize(8.5f).Bold().FontColor(ColorBlanco);
                        c.Item().Padding(10).Column(inner =>
                        {
                            inner.Item().Text(t =>
                            {
                                t.Span("Concepto del pago: ").Bold();
                                t.Span($"Suscripción mensual — Plan {recibo.PlanNombre}");
                            });
                            inner.Item().PaddingTop(3).Text(t =>
                            {
                                t.Span("Referencia / N° de transacción: ").Bold();
                                t.Span(recibo.DocId);
                            });
                            inner.Item().PaddingTop(3).Text(t =>
                            {
                                t.Span("Período: ").Bold();
                                t.Span(periodoStr);
                            });
                        });
                    });

                    col.Item().PaddingTop(8);

                    // ─────────────────────────────────────────────────────────
                    // 4. DETALLE DE CONCEPTOS (tabla)
                    // ─────────────────────────────────────────────────────────
                    col.Item().Border(1f).BorderColor(ColorBorde).Column(c =>
                    {
                        c.Item().Background(ColorNav).PaddingHorizontal(10).PaddingVertical(6)
                         .Text("DETALLE DE CONCEPTOS").FontSize(8.5f).Bold().FontColor(ColorBlanco);

                        c.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(30);    // N°
                                cols.RelativeColumn(4.5f);  // Descripción
                                cols.RelativeColumn(1.1f);  // Cantidad
                                cols.RelativeColumn(1.8f);  // Precio Unit.
                                cols.RelativeColumn(1.8f);  // Importe
                            });

                            void Th(string txt, bool right = false, bool center = false)
                            {
                                var cell = table.Cell()
                                    .Background("#e8ecef")
                                    .BorderBottom(1f).BorderColor(ColorBorde)
                                    .PaddingVertical(7).PaddingHorizontal(6);
                                var t = cell.Text(txt).FontSize(8.5f).Bold().FontColor(ColorNav);
                                if (right) t.AlignRight();
                                else if (center) t.AlignCenter();
                            }

                            Th("N°", center: true);
                            Th("Descripción");
                            Th("Cantidad", center: true);
                            Th("Precio Unitario", right: true);
                            Th("Importe", right: true);

                            // Fila
                            table.Cell().BorderBottom(0.5f).BorderColor(ColorBorde)
                                 .PaddingVertical(10).PaddingHorizontal(6)
                                 .AlignCenter().Text("1").FontSize(9.5f);

                            table.Cell().BorderBottom(0.5f).BorderColor(ColorBorde)
                                 .PaddingVertical(10).PaddingHorizontal(6)
                                 .Text($"Suscripción mensual — Plan {recibo.PlanNombre}").FontSize(9.5f);

                            table.Cell().BorderBottom(0.5f).BorderColor(ColorBorde)
                                 .PaddingVertical(10).PaddingHorizontal(6)
                                 .AlignCenter().Text("1").FontSize(9.5f);

                            table.Cell().BorderBottom(0.5f).BorderColor(ColorBorde)
                                 .PaddingVertical(10).PaddingHorizontal(6)
                                 .AlignRight().Text(montoFmt).FontSize(9.5f);

                            table.Cell().BorderBottom(0.5f).BorderColor(ColorBorde)
                                 .PaddingVertical(10).PaddingHorizontal(6)
                                 .AlignRight().Text(montoFmt).FontSize(9.5f);
                        });

                        // TOTAL RECIBIDO
                        c.Item().Row(r =>
                        {
                            r.RelativeItem();
                            r.ConstantItem(210).Background(ColorNav)
                             .PaddingHorizontal(10).PaddingVertical(8)
                             .Row(tr =>
                             {
                                 tr.RelativeItem()
                                   .Text("TOTAL RECIBIDO:").FontSize(9f).Bold().FontColor(ColorBlanco);
                                 tr.AutoItem()
                                   .Text(montoFmt).FontSize(10.5f).Bold().FontColor(ColorBlanco);
                             });
                        });

                        // Moneda + letras
                        c.Item().BorderTop(0.5f).BorderColor(ColorBorde).Padding(10).Column(inner =>
                        {
                            inner.Item().Text(t => { t.Span("Moneda: ").Bold(); t.Span("Guaraníes (Gs.)"); });
                            inner.Item().PaddingTop(3).Text(t =>
                            {
                                t.Span("Importe en letras: ").Bold();
                                t.Span(importeLetra + ".");
                            });
                        });
                    });

                    col.Item().PaddingTop(8);

                    // ─────────────────────────────────────────────────────────
                    // 5. FORMA DE PAGO
                    // ─────────────────────────────────────────────────────────
                    col.Item().Border(1f).BorderColor(ColorBorde).Column(c =>
                    {
                        c.Item().Background(ColorNav).PaddingHorizontal(10).PaddingVertical(6)
                         .Text("FORMA DE PAGO").FontSize(8.5f).Bold().FontColor(ColorBlanco);
                        c.Item().Padding(10).Row(row =>
                        {
                            var metodos = new[] { "Efectivo", "Transferencia bancaria", "Tarjeta de crédito/débito", "AdamsPay", "PayPal", "Otro" };
                            foreach (var m in metodos)
                            {
                                var sel = string.Equals(m, recibo.MetodoPago, StringComparison.OrdinalIgnoreCase);
                                row.AutoItem().PaddingRight(16).Column(mc =>
                                {
                                    mc.Item().Text(sel ? "[X]" : "[ ]")
                                      .FontSize(9.5f).FontColor(sel ? ColorVerde : ColorSuave).Bold();
                                    mc.Item().PaddingTop(1).Text(m)
                                      .FontSize(8f).FontColor(sel ? ColorNav : ColorSuave);
                                });
                            }
                        });
                    });

                    col.Item().PaddingTop(8);

                    // ─────────────────────────────────────────────────────────
                    // 6. INFORMACIÓN DE TRAZABILIDAD
                    // ─────────────────────────────────────────────────────────
                    col.Item().Border(1f).BorderColor(ColorBorde).Column(c =>
                    {
                        c.Item().Background(ColorNav).PaddingHorizontal(10).PaddingVertical(6)
                         .Text("INFORMACIÓN DE TRAZABILIDAD").FontSize(8.5f).Bold().FontColor(ColorBlanco);
                        c.Item().Padding(10).Text(t =>
                        {
                            t.Span("Fecha y hora del registro en el sistema: ").Bold();
                            t.Span(fechaTraza);
                        });
                    });

                    col.Item().PaddingTop(8);

                    // ─────────────────────────────────────────────────────────
                    // 7. LEYENDA
                    // ─────────────────────────────────────────────────────────
                    col.Item().Background(ColorFondoS).Border(1f).BorderColor(ColorAzul)
                       .Padding(10).Row(row =>
                       {
                           row.ConstantItem(22).AlignCenter().AlignMiddle()
                              .Text("[*]").FontSize(9f).FontColor(ColorAzul).Bold();
                           row.RelativeItem().AlignMiddle()
                              .Text($"Este recibo electrónico acredita únicamente la recepción del importe indicado " +
                                    $"y ha sido generado por el {SistemaLeyenda}.")
                              .FontSize(8.5f).FontColor(ColorNav).Italic();
                       });
                });

                // ── FOOTER ────────────────────────────────────────────────────
                page.Footer()
                    .BorderTop(0.5f).BorderColor(ColorBorde)
                    .PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem()
                         .Text($"{EmisorNombre}  ·  {EmisorEmpresa}  ·  {EmisorEmail}")
                         .FontSize(7.5f).FontColor("#aaaaaa");
                        r.AutoItem().AlignRight()
                         .Text("Página 1 de 1")
                         .FontSize(7.5f).FontColor("#aaaaaa");
                    });
            });
        });

        return doc.GeneratePdf();
    }

    // ── Conversión de número a letras (guaraníes) ─────────────────────────────

    private static string NumeroALetras(decimal monto)
    {
        long n = (long)Math.Abs(monto);
        return n == 0 ? "cero guaraníes" : $"{ConvertirNumero(n)} guaraníes";
    }

    private static string Capitalizar(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private static readonly string[] Unidades =
        ["", "un", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve"];
    private static readonly string[] Decenas =
        ["", "diez", "veinte", "treinta", "cuarenta", "cincuenta",
         "sesenta", "setenta", "ochenta", "noventa"];
    private static readonly string[] Centenas =
        ["", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos",
         "seiscientos", "setecientos", "ochocientos", "novecientos"];
    private static readonly string[] Teens =
        ["diez", "once", "doce", "trece", "catorce", "quince",
         "dieciséis", "diecisiete", "dieciocho", "diecinueve"];

    private static string ConvertirNumero(long n)
    {
        if (n == 0) return "cero";
        var sb = new System.Text.StringBuilder();

        if (n >= 1_000_000)
        {
            long mill = n / 1_000_000;
            sb.Append(mill == 1 ? "un millón" : ConvertirNumero(mill) + " millones");
            n %= 1_000_000;
            if (n > 0) sb.Append(' ');
        }
        if (n >= 1_000)
        {
            long miles = n / 1_000;
            sb.Append(miles == 1 ? "mil" : ConvertirNumero(miles) + " mil");
            n %= 1_000;
            if (n > 0) sb.Append(' ');
        }
        if (n >= 100)
        {
            int c = (int)(n / 100);
            n %= 100;
            sb.Append(c == 1 && n == 0 ? "cien" : Centenas[c]);
            if (n > 0) sb.Append(' ');
        }
        if (n >= 10 && n < 20)
        {
            sb.Append(Teens[(int)(n - 10)]);
        }
        else if (n >= 20)
        {
            int d = (int)(n / 10);
            n %= 10;
            if (d == 2 && n > 0)      sb.Append("veinti" + Unidades[(int)n]);
            else if (n > 0)           sb.Append(Decenas[d] + " y " + Unidades[(int)n]);
            else                      sb.Append(Decenas[d]);
        }
        else if (n > 0)
        {
            sb.Append(Unidades[(int)n]);
        }

        return sb.ToString().Trim();
    }
}
