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
    private const string EmisorNombre  = "SAD-Roshka";
    private const string EmisorEmpresa = "Roshka S.A.";
    private const string EmisorDir1   = "Aviadores del Chaco 1669";
    private const string EmisorDir2   = "Asunción, Paraguay";
    private const string EmisorEmail  = "soporte@roshka.com";

    public byte[] GenerarReciboPdf(ReciboData recibo)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var esPY     = new System.Globalization.CultureInfo("es-PY");
        var montoStr = recibo.Monto.ToString("N0", esPY);
        var montoFmt = $"Gs. {montoStr}";

        var fechaPagoFull = recibo.FechaPago.ToLocalTime()
                                  .ToString("dd 'de' MMMM 'de' yyyy", esPY);
        var fechaPagoCorta = recibo.FechaPago.ToLocalTime()
                                   .ToString("dd MMM yyyy", esPY);

        var fechaInicioStr = recibo.FechaInicio.HasValue
            ? recibo.FechaInicio.Value.ToLocalTime().ToString("dd/MM/yyyy") : "—";
        var fechaVencStr = recibo.FechaVencimiento.HasValue
            ? recibo.FechaVencimiento.Value.ToLocalTime().ToString("dd/MM/yyyy") : "—";
        var periodoStr = (recibo.FechaInicio.HasValue && recibo.FechaVencimiento.HasValue)
            ? $"{recibo.FechaInicio.Value.ToLocalTime():dd/MM} – {recibo.FechaVencimiento.Value.ToLocalTime():dd/MM/yyyy}"
            : $"{fechaInicioStr} – {fechaVencStr}";

        // N° factura: versión más corta del docId
        var nroFactura = recibo.DocId.Replace("SAD-", "RKA-").Replace("-202", "-");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(2.5f, Unit.Centimetre);
                page.MarginVertical(2.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial").FontColor("#1a1a1a"));

                page.Content().Column(col =>
                {
                    // ── ENCABEZADO: Título + Logo ─────────────────────────────
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Recibo")
                           .FontSize(26).Bold().FontColor("#1a1a1a");

                        row.AutoItem().AlignRight().AlignBottom().Column(c =>
                        {
                            c.Item().Text("SAD")
                             .FontSize(20).Bold().FontColor("#1c2b3a").AlignRight();
                            c.Item().Text("Sistema de Apoyo a la Decisión")
                             .FontSize(7f).FontColor("#7e9ab2").AlignRight();
                        });
                    });

                    col.Item().PaddingTop(18).Column(meta =>
                    {
                        void MetaRow(string label, string valor)
                        {
                            meta.Item().PaddingBottom(3).Row(r =>
                            {
                                r.ConstantItem(115).Text(label)
                                 .FontSize(9.5f).FontColor("#666666");
                                r.RelativeItem().Text(valor)
                                 .FontSize(9.5f).FontColor("#1a1a1a");
                            });
                        }
                        MetaRow("N° de factura", nroFactura);
                        MetaRow("N° de recibo",  recibo.DocId);
                        MetaRow("Fecha de pago", fechaPagoFull);
                    });

                    col.Item().PaddingTop(22).LineHorizontal(0.75f).LineColor("#d8d8d8");
                    col.Item().PaddingTop(18);

                    // ── DOS COLUMNAS: Emisor | Facturado a ────────────────────
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(EmisorNombre).Bold().FontSize(10.5f);
                            c.Item().PaddingTop(4).Text(EmisorEmpresa)
                             .FontSize(9.5f).FontColor("#444444");
                            c.Item().Text(EmisorDir1)
                             .FontSize(9.5f).FontColor("#444444");
                            c.Item().Text(EmisorDir2)
                             .FontSize(9.5f).FontColor("#444444");
                            c.Item().Text(EmisorEmail)
                             .FontSize(9.5f).FontColor("#444444");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Facturado a").Bold().FontSize(10.5f);
                            c.Item().PaddingTop(4);

                            if (!string.IsNullOrWhiteSpace(recibo.NombreCliente))
                                c.Item().Text(recibo.NombreCliente)
                                 .FontSize(9.5f).FontColor("#444444");

                            c.Item().Text("Roshka S.A.")
                             .FontSize(9.5f).FontColor("#444444");
                            c.Item().Text("Paraguay")
                             .FontSize(9.5f).FontColor("#444444");

                            if (!string.IsNullOrWhiteSpace(recibo.EmailCliente))
                                c.Item().Text(recibo.EmailCliente)
                                 .FontSize(9.5f).FontColor("#444444");
                        });
                    });

                    col.Item().PaddingTop(26);

                    // ── MONTO DESTACADO ────────────────────────────────────────
                    col.Item().Text($"{montoFmt} pagado el {fechaPagoFull}")
                       .FontSize(17).Bold().FontColor("#1a1a1a");

                    col.Item().PaddingTop(26).LineHorizontal(0.75f).LineColor("#d8d8d8");

                    // ── TABLA DE LÍNEAS ────────────────────────────────────────
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4f);   // Descripción
                            c.RelativeColumn(0.8f); // Cant.
                            c.RelativeColumn(1.6f); // Precio unit.
                            c.RelativeColumn(1.6f); // Total
                        });

                        void Th(string txt, bool right = false)
                        {
                            var cell = table.Cell()
                                .BorderBottom(0.75f).BorderColor("#d8d8d8")
                                .PaddingVertical(8).PaddingHorizontal(2);
                            var t = cell.Text(txt).FontSize(9f).FontColor("#888888");
                            if (right) t.AlignRight();
                        }

                        Th("Descripción");
                        Th("Cant.", right: true);
                        Th("Precio unit.", right: true);
                        Th("Total", right: true);

                        // Fila de producto
                        table.Cell().PaddingVertical(14).PaddingHorizontal(2).Column(c =>
                        {
                            c.Item().Text($"Plan {recibo.PlanNombre}")
                             .Bold().FontSize(10f);
                            c.Item().PaddingTop(2).Text(periodoStr)
                             .FontSize(9f).FontColor("#888888");
                        });

                        table.Cell().PaddingVertical(14).AlignRight()
                             .Text("1").FontSize(10f);

                        table.Cell().PaddingVertical(14).AlignRight()
                             .Text(montoFmt).FontSize(10f);

                        table.Cell().PaddingVertical(14).AlignRight()
                             .Text(montoFmt).FontSize(10f);
                    });

                    col.Item().LineHorizontal(0.75f).LineColor("#d8d8d8");
                    col.Item().PaddingTop(10);

                    // ── TOTALES ────────────────────────────────────────────────
                    void TotalFila(string label, string valor, bool negrita = false, bool separador = false)
                    {
                        if (separador)
                            col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor("#d8d8d8");

                        col.Item().PaddingVertical(4).Row(r =>
                        {
                            r.RelativeItem();

                            var labelCell = r.ConstantItem(130).AlignRight();
                            if (negrita)
                                labelCell.Text(label).FontSize(9.5f).FontColor("#1a1a1a").Bold();
                            else
                                labelCell.Text(label).FontSize(9.5f).FontColor("#666666");

                            var valorCell = r.ConstantItem(110).AlignRight();
                            if (negrita)
                                valorCell.Text(valor).FontSize(11f).FontColor("#1a1a1a").Bold();
                            else
                                valorCell.Text(valor).FontSize(9.5f).FontColor("#1a1a1a");
                        });
                    }

                    TotalFila("Subtotal",      montoFmt);
                    TotalFila("Total",         montoFmt);
                    TotalFila("Monto pagado",  montoFmt, negrita: true, separador: true);

                    col.Item().PaddingTop(34);

                    // ── HISTORIAL DE PAGOS ─────────────────────────────────────
                    col.Item().Text("Historial de pagos")
                       .FontSize(13).Bold().FontColor("#1a1a1a");

                    col.Item().PaddingTop(14).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.4f); // Método
                            c.RelativeColumn(1.4f); // Fecha
                            c.RelativeColumn(1.4f); // Monto
                            c.RelativeColumn(2f);   // N° recibo
                        });

                        void Hh(string txt)
                        {
                            table.Cell()
                                .BorderBottom(0.75f).BorderColor("#d8d8d8")
                                .PaddingVertical(7)
                                .Text(txt).FontSize(9f).FontColor("#888888");
                        }

                        Hh("Método de pago");
                        Hh("Fecha");
                        Hh("Monto pagado");
                        Hh("N° de recibo");

                        table.Cell().PaddingVertical(10).Text("AdamsPay").FontSize(9.5f);
                        table.Cell().PaddingVertical(10).Text(fechaPagoCorta).FontSize(9.5f);
                        table.Cell().PaddingVertical(10).Text(montoFmt).FontSize(9.5f);
                        table.Cell().PaddingVertical(10).Text(recibo.DocId)
                             .FontSize(9f).FontColor("#444444");
                    });

                    col.Item().PaddingTop(30).LineHorizontal(0.5f).LineColor("#e8e8e8");
                    col.Item().PaddingTop(12)
                       .Text("Este documento es un comprobante válido de pago. Consérvelo para sus registros.")
                       .FontSize(8f).FontColor("#999999").Italic();
                });

                // ── FOOTER ─────────────────────────────────────────────────────
                page.Footer().Row(r =>
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
}
