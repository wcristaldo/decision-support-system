using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace DecisionSupportAPI.Services;

// ── DTO compartido entre EmailService y ReceiptService ───────────────────────
public record ReciboData(
    string    DocId,
    string    PlanNombre,
    decimal   Monto,
    DateTime  FechaPago,
    DateTime? FechaInicio,
    DateTime? FechaVencimiento,
    string?   NombreCliente    = null,
    string?   EmailCliente     = null,
    string    NumeroRecibo     = "REC-000000",
    DateTime? FechaEmision     = null,          // null → usa FechaPago
    string    MetodoPago       = "AdamsPay",
    string    EstadoRecibo     = "Emitido",
    string?   DocumentoCliente = null,
    string?   TelefonoCliente  = null,
    string?   DireccionCliente = null
);

// ── Interfaz ─────────────────────────────────────────────────────────────────
public interface IEmailService
{
    Task EnviarReciboAsync(ReciboData recibo, List<string> destinatarios, byte[] pdfBytes);

    /// <summary>
    /// Envía una alerta automática a los administradores cuando un resultado
    /// genera recomendación "No Apto para despliegue".
    /// Requiere que el plan activo tenga NotificacionesEmail = true.
    /// </summary>
    Task EnviarAlertaNoAptoAsync(
        string proyectoNombre,
        string versionNumero,
        string archivoNombre,
        List<string> destinatarios);
}

// ── Implementación ────────────────────────────────────────────────────────────
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task EnviarReciboAsync(ReciboData recibo, List<string> destinatarios, byte[] pdfBytes)
    {
        var host     = _config["Email:SmtpHost"]     ?? "smtp.gmail.com";
        var port     = int.Parse(_config["Email:SmtpPort"] ?? "587");
        var user     = _config["Email:Username"]     ?? "";
        var password = _config["Email:Password"]     ?? "";
        var from     = _config["Email:FromAddress"]  ?? user;
        var fromName = _config["Email:FromName"]     ?? "Roshka DSS";

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("Email no configurado — omitiendo envío de recibo.");
            return;
        }

        var gsFormat   = new System.Globalization.CultureInfo("es-PY");
        var montoStr   = recibo.Monto.ToString("N0", gsFormat);

        // Pre-computar expresiones complejas para evitar errores en el raw string literal
        var fechaInicio = recibo.FechaInicio.HasValue
            ? recibo.FechaInicio.Value.ToLocalTime().ToString("dd/MM/yyyy")
            : "—";
        var fechaVenc = recibo.FechaVencimiento.HasValue
            ? recibo.FechaVencimiento.Value.ToLocalTime().ToString("dd/MM/yyyy")
            : "—";
        var fechaPagoStr = recibo.FechaPago.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        var anio         = DateTime.UtcNow.Year;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, from));
        foreach (var dest in destinatarios)
            message.To.Add(MailboxAddress.Parse(dest));

        message.Subject = $"Recibo de pago — Plan {recibo.PlanNombre} — Roshka DSS";

        // ── Body HTML ($$""" = doble $: CSS usa { } literal, interpolaciones usan {{expr}}) ──
        var html = $$"""
            <!DOCTYPE html>
            <html lang="es">
            <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Recibo de pago</title>
            <style>
              body { font-family: 'Segoe UI', Arial, sans-serif; background: #f5f7fa; margin: 0; padding: 0; }
              .wrap { max-width: 560px; margin: 32px auto; background: #fff; border-radius: 12px;
                      box-shadow: 0 4px 20px rgba(0,0,0,0.10); overflow: hidden; }
              .header { background: #1c2b3a; padding: 28px 32px; color: #fff; }
              .header h1 { margin: 0; font-size: 1.3rem; font-weight: 700; letter-spacing: 0.5px; }
              .header p { margin: 4px 0 0; font-size: 0.85rem; color: #7e9ab2; }
              .badge-aprobado { display: inline-block; background: #22c55e; color: #fff;
                                border-radius: 20px; padding: 3px 12px; font-size: 0.8rem;
                                font-weight: 600; margin-top: 12px; text-transform: uppercase; letter-spacing: 1px; }
              .body { padding: 28px 32px; }
              .intro { color: #4a647a; font-size: 0.92rem; margin-bottom: 24px; }
              table { width: 100%; border-collapse: collapse; margin-bottom: 24px; }
              td { padding: 10px 0; border-bottom: 1px solid #f0f4f8; font-size: 0.91rem; color: #1c2b3a; }
              td:first-child { color: #7e9ab2; width: 50%; }
              .total-row td { border-bottom: none; padding-top: 16px; font-weight: 700; font-size: 1rem; }
              .total-row td:last-child { color: #1c2b3a; font-size: 1.15rem; }
              .divider { border: none; border-top: 2px solid #f0f4f8; margin: 0 0 16px; }
              .footer { background: #f5f7fa; padding: 20px 32px; text-align: center;
                        font-size: 0.8rem; color: #7e9ab2; }
              .footer a { color: #3b82f6; text-decoration: none; }
            </style></head>
            <body>
            <div class="wrap">
              <div class="header">
                <h1>Roshka DSS · Decision Support System</h1>
                <p>Comprobante de pago de suscripción</p>
                <span class="badge-aprobado">&#10003; Pago aprobado</span>
              </div>
              <div class="body">
                <p class="intro">Tu suscripción ha sido activada exitosamente. A continuación encontrarás los detalles del pago.</p>
                <table>
                  <tr><td>N° de recibo</td><td><strong>{{recibo.DocId}}</strong></td></tr>
                  <tr><td>Fecha de pago</td><td>{{fechaPagoStr}}</td></tr>
                  <tr><td>Plan contratado</td><td>{{recibo.PlanNombre}}</td></tr>
                  <tr><td>Inicio de vigencia</td><td>{{fechaInicio}}</td></tr>
                  <tr><td>Vencimiento</td><td>{{fechaVenc}}</td></tr>
                  <tr><td>Método de pago</td><td>{{recibo.MetodoPago}}</td></tr>
                  <tr><td colspan="2"><hr class="divider" /></td></tr>
                  <tr class="total-row"><td>Total pagado</td><td>Gs. {{montoStr}}</td></tr>
                </table>
                <p style="color:#7e9ab2;font-size:0.82rem;margin-top:8px;">
                  El recibo electrónico en PDF se adjunta a este correo. Conservalo para tus registros.
                </p>
              </div>
              <div class="footer">
                Roshka DSS &copy; {{anio}} &nbsp;|&nbsp; soporte-dss@roshka.com
              </div>
            </div>
            </body></html>
            """;

        var bodyBuilder = new BodyBuilder { HtmlBody = html };

        // Adjuntar PDF
        bodyBuilder.Attachments.Add(
            $"Recibo_{recibo.DocId}.pdf",
            pdfBytes,
            new ContentType("application", "pdf"));

        message.Body = bodyBuilder.ToMessageBody();

        // ── Envío via SMTP ───────────────────────────────────────────────────
        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(user, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task EnviarAlertaNoAptoAsync(
        string proyectoNombre,
        string versionNumero,
        string archivoNombre,
        List<string> destinatarios)
    {
        if (destinatarios.Count == 0) return;

        var host     = _config["Email:SmtpHost"]    ?? "";
        var port     = int.Parse(_config["Email:SmtpPort"] ?? "587");
        var user     = _config["Email:Username"]    ?? "";
        var password = _config["Email:Password"]    ?? "";
        var from     = _config["Email:FromAddress"] ?? user;
        var fromName = _config["Email:FromName"]    ?? "Roshka DSS";

        var fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        var anio  = DateTime.Now.Year;

        // ── Body HTML ($$""" = doble $: CSS usa { } literal, interpolaciones usan {{expr}}) ──
        var html = $$"""
            <!DOCTYPE html><html><head><meta charset="utf-8">
            <style>
              body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; margin: 0; background: #f5f7fa; }
              .wrap { max-width: 560px; margin: 32px auto; background: #fff; border-radius: 12px; overflow: hidden; box-shadow: 0 2px 12px rgba(0,0,0,.08); }
              .header { background: #c0392b; color: #fff; padding: 28px 32px; }
              .header h1 { margin: 0 0 4px; font-size: 1.1rem; font-weight: 600; }
              .header p { margin: 0; font-size: 0.85rem; opacity: .85; }
              .badge { display: inline-block; background: #fff; color: #c0392b; font-weight: 700; font-size: 0.78rem; padding: 4px 12px; border-radius: 20px; margin-top: 12px; letter-spacing: .5px; }
              .body { padding: 28px 32px; }
              .intro { color: #4a647a; font-size: 0.92rem; margin-bottom: 24px; }
              table { width: 100%; border-collapse: collapse; margin-bottom: 24px; }
              td { padding: 10px 0; border-bottom: 1px solid #f0f4f8; font-size: 0.91rem; color: #1c2b3a; }
              td:first-child { color: #7e9ab2; width: 45%; }
              .alerta { background: #fef2f2; border: 1px solid #fecaca; border-radius: 8px; padding: 14px 18px; margin-bottom: 20px; color: #7f1d1d; font-size: 0.9rem; }
              .footer { background: #f5f7fa; padding: 20px 32px; text-align: center; font-size: 0.8rem; color: #7e9ab2; }
            </style></head>
            <body>
            <div class="wrap">
              <div class="header">
                <h1>Roshka DSS · Alerta de Calidad</h1>
                <p>Notificación automática del sistema</p>
                <span class="badge">&#9888; No Apto para Despliegue</span>
              </div>
              <div class="body">
                <div class="alerta">
                  El motor de recomendación ha detectado que los resultados de prueba no cumplen los umbrales mínimos de calidad configurados para este proyecto.
                </div>
                <table>
                  <tr><td>Proyecto</td><td><strong>{{proyectoNombre}}</strong></td></tr>
                  <tr><td>Versión</td><td>{{versionNumero}}</td></tr>
                  <tr><td>Archivo evaluado</td><td>{{archivoNombre}}</td></tr>
                  <tr><td>Fecha de evaluación</td><td>{{fecha}}</td></tr>
                  <tr><td>Recomendación</td><td><strong style="color:#c0392b">No Apto para Despliegue</strong></td></tr>
                </table>
                <p style="color:#7e9ab2;font-size:0.82rem;">
                  Ingresá al sistema para revisar las métricas en detalle y registrar la decisión de despliegue correspondiente.
                </p>
              </div>
              <div class="footer">
                Roshka DSS &copy; {{anio}} &nbsp;|&nbsp; Notificación automática — no responder a este correo.
              </div>
            </div>
            </body></html>
            """;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, from));
        foreach (var dest in destinatarios)
            message.To.Add(MailboxAddress.Parse(dest));
        message.Subject = $"[Roshka DSS] ⚠ No Apto para Despliegue — {proyectoNombre} v{versionNumero}";
        message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(user, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
