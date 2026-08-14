using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;
using System.Text.Json;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuscripcionController : ControllerBase
{
    private readonly ApplicationDbContext           _db;
    private readonly ISuscripcionService            _suscripcionService;
    private readonly IAdamsPayService               _adams;
    private readonly IPayPalService                 _paypal;
    private readonly IEmailService                  _email;
    private readonly IReceiptService                _receipt;
    private readonly ILogger<SuscripcionController> _logger;

    public SuscripcionController(
        ApplicationDbContext            db,
        ISuscripcionService             suscripcionService,
        IAdamsPayService                adams,
        IPayPalService                  paypal,
        IEmailService                   email,
        IReceiptService                 receipt,
        ILogger<SuscripcionController>  logger)
    {
        _db                 = db;
        _suscripcionService = suscripcionService;
        _adams              = adams;
        _paypal             = paypal;
        _email              = email;
        _receipt            = receipt;
        _logger             = logger;
    }

    // ── GET /api/suscripcion/planes ──────────────────────────────────────
    [HttpGet("planes")]
    [Authorize]
    public async Task<IActionResult> GetPlanes()
    {
        var planes = await _db.PlanesSuscripcion
            .Where(p => p.Estado == "activo")
            .OrderBy(p => p.PrecioMensual)
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                p.PrecioMensual,
                Limites = new
                {
                    MaxProyectos        = p.MaxProyectos        == null ? "Ilimitado"  : p.MaxProyectos.ToString(),
                    MaxUsuarios         = p.MaxUsuarios         == null ? "Ilimitado"  : p.MaxUsuarios.ToString(),
                    MaxEvaluacionesMes  = p.MaxEvaluacionesMes  == null ? "Ilimitadas" : p.MaxEvaluacionesMes.ToString(),
                    MaxTamanoArchivoMb  = p.MaxTamanoArchivoMb,
                    HistorialDias       = p.HistorialDias == null ? "Completo" : $"Últimos {p.HistorialDias} días",
                },
                Funcionalidades = new
                {
                    p.ExportarPdf,
                    p.ExportarExcel,
                    p.DashboardAvanzado,
                    p.AuditoriaDetallada,
                    p.NotificacionesEmail,
                    p.NotificacionesSlack,
                    p.ApiPublica,
                    p.IntegracionCicd,
                    p.Webhooks,
                    p.SoportePrioritario,
                }
            })
            .ToListAsync();

        return Ok(planes);
    }

    // ── GET /api/suscripcion/actual ──────────────────────────────────────
    [HttpGet("actual")]
    [Authorize]
    public async Task<IActionResult> GetActual()
    {
        var sub = await _suscripcionService.GetSuscripcionActivaAsync();

        if (sub == null)
            return Ok(new { activa = false, mensaje = "Sin suscripción activa." });

        var inicioMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var proyectos = await _db.Proyectos.CountAsync(p => p.Estado == "activo");
        var usuarios  = await _db.Usuarios.CountAsync(u => u.Estado == "activo");
        var evalMes   = await _db.ResultadosPrueba.CountAsync(r => r.FechaCarga >= inicioMes);

        // Días restantes para vencimiento
        int? diasRestantes = sub.FechaVencimiento.HasValue
            ? Math.Max(0, (sub.FechaVencimiento.Value.Date - DateTime.UtcNow.Date).Days)
            : null;

        return Ok(new
        {
            activa           = true,
            id               = sub.Id,
            plan             = new { sub.Plan!.Id, sub.Plan.Nombre, sub.Plan.PrecioMensual },
            estado           = sub.Estado,
            fechaInicio      = sub.FechaInicio,
            fechaVencimiento = sub.FechaVencimiento,
            diasRestantes,
            usoActual = new
            {
                proyectos,
                maxProyectos       = sub.Plan.MaxProyectos,
                usuarios,
                maxUsuarios        = sub.Plan.MaxUsuarios,
                evaluacionesMes    = evalMes,
                maxEvaluacionesMes = sub.Plan.MaxEvaluacionesMes,
            }
        });
    }

    // ── POST /api/suscripcion/iniciar-pago ───────────────────────────────
    [HttpPost("iniciar-pago")]
    [Authorize]
    public async Task<IActionResult> IniciarPago([FromBody] IniciarPagoRequest req)
    {
        if (!User.IsInRole("Administrador")) return Forbid();

        var plan = await _db.PlanesSuscripcion.FindAsync(req.IdPlan);
        if (plan == null)
            return NotFound(new { message = "Plan no encontrado." });

        var docId = $"DSS-{plan.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var label = $"Roshka DSS — Plan {plan.Nombre} ({DateTime.UtcNow:MM/yyyy})";

        var (success, payUrl, error) = await _adams.CreateDebtAsync(docId, label, plan.PrecioMensual, validHours: 24);

        if (!success || payUrl == null)
            return BadRequest(new { message = $"Error al crear deuda en AdamsPay: {error}" });

        // Si hay suscripción activa, crear una nueva pendiente sin tocarla.
        // Si solo hay pendiente, reutilizarla. Si no hay ninguna, crear nueva.
        var subExistente = await _db.Suscripciones
            .Where(s => s.Estado == "pendiente" || s.Estado == "activa")
            .OrderByDescending(s => s.FechaCreacion)
            .FirstOrDefaultAsync();

        Suscripcion sub;
        if (subExistente == null || subExistente.Estado == "activa")
        {
            sub = new Suscripcion { IdPlan = plan.Id, Estado = "pendiente", FechaCreacion = DateTime.UtcNow };
            _db.Suscripciones.Add(sub);
        }
        else
        {
            sub = subExistente;
            sub.IdPlan = plan.Id;
        }

        await _db.SaveChangesAsync();

        // Registrar intento de pago
        // PagoparIdPedidoComercio → Adams docId  |  PagoparHashPedido → Adams payUrl
        _db.PagosSuscripcion.Add(new PagoSuscripcion
        {
            IdSuscripcion           = sub.Id,
            IdPlan                  = plan.Id,
            Monto                   = plan.PrecioMensual,
            Estado                  = "pendiente",
            PagoparIdPedidoComercio = docId,
            PagoparHashPedido       = payUrl,
            FechaCreacion           = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return Ok(new { payUrl });
    }

    // ── POST /api/suscripcion/webhook/adams ──────────────────────────────
    /// <summary>
    /// Webhook de AdamsPay. Valida HMAC, activa suscripción y envía recibo por email.
    /// Configurar en AdamsPay → Aplicaciones → Webhook.
    /// Siempre retorna HTTP 200.
    /// </summary>
    [HttpPost("webhook/adams")]
    [AllowAnonymous]
    public async Task<IActionResult> WebhookAdams()
    {
        string body;
        using (var reader = new System.IO.StreamReader(Request.Body))
            body = await reader.ReadToEndAsync();

        _logger.LogInformation("Webhook AdamsPay recibido: {Body}", body);

        var hmacRecibido = Request.Headers["x-adams-notify-hash"].FirstOrDefault() ?? "";
        if (!_adams.ValidateWebhookHmac(body, hmacRecibido))
        {
            _logger.LogWarning("Webhook AdamsPay con HMAC inválido. Recibido: {Hmac}", hmacRecibido);
            return Ok();
        }

        try
        {
            var doc  = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("notify", out var notify)) return Ok();
            var eventType = notify.GetProperty("type").GetString();
            if (eventType != "debtStatus") return Ok();

            if (!root.TryGetProperty("debt", out var debt)) return Ok();

            var payStatus = debt.GetProperty("payStatus").GetProperty("status").GetString();
            var docId     = debt.GetProperty("docId").GetString();

            var pago = await _db.PagosSuscripcion
                .Include(p => p.Suscripcion)
                .Include(p => p.Plan)
                .FirstOrDefaultAsync(p => p.PagoparIdPedidoComercio == docId);

            if (pago == null)
            {
                _logger.LogWarning("Webhook Adams: pago no encontrado para docId {DocId}", docId);
                return Ok();
            }

            pago.PagoparRespuesta = body;

            if (payStatus == "paid")
            {
                pago.Estado    = "aprobado";
                pago.FechaPago = DateTime.UtcNow;

                if (pago.Suscripcion != null)
                {
                    pago.Suscripcion.IdPlan           = pago.IdPlan;
                    pago.Suscripcion.Estado           = "activa";
                    pago.Suscripcion.FechaInicio      = DateTime.UtcNow;
                    pago.Suscripcion.FechaVencimiento = DateTime.UtcNow.AddMonths(1);
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("Suscripción activada via AdamsPay, docId {DocId}", docId);

                // ── Enviar recibo por email ────────────────────────────────────
                try
                {
                    var admins = await _db.Usuarios
                        .Where(u => u.Estado == "activo"
                                 && u.UsuarioRoles.Any(ur => ur.Estado == "activo"
                                                          && ur.Rol!.NombreRol == "Administrador"))
                        .Select(u => new { u.Nombre, u.Apellido, u.Email })
                        .ToListAsync();

                    var adminEmails = admins.Select(a => a.Email).ToList();

                    if (adminEmails.Count > 0)
                    {
                        var primerAdmin = admins.First();
                        var recibo = new ReciboData(
                            DocId:            docId!,
                            PlanNombre:       pago.Plan?.Nombre ?? "—",
                            Monto:            pago.Monto,
                            FechaPago:        pago.FechaPago.Value,
                            FechaInicio:      pago.Suscripcion?.FechaInicio,
                            FechaVencimiento: pago.Suscripcion?.FechaVencimiento,
                            NombreCliente:    $"{primerAdmin.Nombre} {primerAdmin.Apellido}".Trim(),
                            EmailCliente:     primerAdmin.Email
                        );

                        var pdfBytes = _receipt.GenerarReciboPdf(recibo);
                        await _email.EnviarReciboAsync(recibo, adminEmails, pdfBytes);
                        _logger.LogInformation("Recibo enviado a {Count} admin(s) para docId {DocId}", adminEmails.Count, docId);
                    }
                }
                catch (Exception ex)
                {
                    // El fallo de email no debe romper el webhook
                    _logger.LogError(ex, "Error enviando recibo de pago para docId {DocId}", docId);
                }
            }
            else
            {
                pago.Estado = "revertido";

                if (pago.Suscripcion?.Estado == "activa")
                    pago.Suscripcion.Estado = "vencida";

                await _db.SaveChangesAsync();

                _logger.LogInformation("Pago revertido/cancelado en AdamsPay, docId {DocId}", docId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando webhook AdamsPay");
        }

        return Ok();
    }

    // ── GET /api/suscripcion/pagos ────────────────────────────────────────
    [HttpGet("pagos")]
    [Authorize]
    public async Task<IActionResult> GetPagos()
    {
        if (!User.IsInRole("Administrador")) return Forbid();

        var pagos = await _db.PagosSuscripcion
            .Include(p => p.Plan)
            .Include(p => p.Suscripcion)
            .OrderByDescending(p => p.FechaCreacion)
            .Select(p => new
            {
                p.Id,
                p.Monto,
                p.Estado,
                Plan              = p.Plan!.Nombre,
                Referencia        = p.PagoparIdPedidoComercio,
                p.FechaPago,
                p.FechaCreacion,
                FechaVencimiento  = (DateTime?)p.Suscripcion!.FechaVencimiento,
            })
            .ToListAsync();

        return Ok(pagos);
    }

    // ── POST /api/suscripcion/cambiar-plan ────────────────────────────────
    [HttpPost("cambiar-plan")]
    [Authorize]
    public async Task<IActionResult> CambiarPlan([FromBody] CambiarPlanRequest req)
    {
        if (!User.IsInRole("Administrador")) return Forbid();

        var plan = await _db.PlanesSuscripcion.FindAsync(req.IdPlan);
        if (plan == null)
            return NotFound(new { message = "Plan no encontrado." });

        var sub = await _suscripcionService.GetSuscripcionActivaAsync();
        if (sub == null)
            return BadRequest(new { message = "No hay suscripción activa para cambiar." });

        sub.IdPlan = plan.Id;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Plan cambiado a {plan.Nombre}." });
    }

    // ── POST /api/suscripcion/test-email ─────────────────────────────────
    /// <summary>Endpoint de diagnóstico — envía un email de prueba y expone el error si falla.</summary>
    [HttpPost("test-email")]
    [Authorize]
    public async Task<IActionResult> TestEmail()
    {
        if (!User.IsInRole("Administrador")) return Forbid();

        var adminEmails = await _db.Usuarios
            .Where(u => u.Estado == "activo"
                     && u.UsuarioRoles.Any(ur => ur.Estado == "activo"
                                              && ur.Rol!.NombreRol == "Administrador"))
            .Select(u => u.Email)
            .ToListAsync();

        if (adminEmails.Count == 0)
            return BadRequest(new { error = "No se encontraron administradores activos en la BD." });

        try
        {
            var recibo = new ReciboData(
                DocId:            "TEST-DIAGNOSTICO",
                PlanNombre:       "Plan de Prueba",
                Monto:            999_000m,
                FechaPago:        DateTime.UtcNow,
                FechaInicio:      DateTime.UtcNow,
                FechaVencimiento: DateTime.UtcNow.AddMonths(1)
            );
            var pdfBytes = _receipt.GenerarReciboPdf(recibo);
            await _email.EnviarReciboAsync(recibo, adminEmails, pdfBytes);

            return Ok(new { message = $"Email enviado correctamente a: {string.Join(", ", adminEmails)}" });
        }
        catch (Exception ex)
        {
            // Expone el error real para diagnóstico
            _logger.LogError(ex, "test-email falló");
            return StatusCode(500, new
            {
                error  = ex.Message,
                tipo   = ex.GetType().Name,
                detalle = ex.InnerException?.Message
            });
        }
    }

    // ── POST /api/suscripcion/iniciar-pago-paypal ────────────────────────────
    /// <summary>
    /// Crea una orden en PayPal y devuelve la URL de aprobación para redirigir al usuario.
    /// FrontendUrl: origen del frontend (ej. http://localhost:5173) para construir returnUrl.
    /// </summary>
    [HttpPost("iniciar-pago-paypal")]
    [Authorize]
    public async Task<IActionResult> IniciarPagoPayPal([FromBody] IniciarPagoPayPalRequest req)
    {
        if (!User.IsInRole("Administrador")) return Forbid();

        var plan = await _db.PlanesSuscripcion.FindAsync(req.IdPlan);
        if (plan == null)
            return NotFound(new { message = "Plan no encontrado." });

        var returnUrl = $"{req.FrontendUrl}/suscripcion?pp_status=success&pp_planId={req.IdPlan}";
        var cancelUrl = $"{req.FrontendUrl}/suscripcion?pp_status=cancel";

        var (success, approvalUrl, orderId, error) = await _paypal.CreateOrderAsync(
            req.IdPlan, plan.Nombre, plan.PrecioMensual, returnUrl, cancelUrl);

        if (!success || approvalUrl == null)
            return BadRequest(new { message = $"Error al crear orden en PayPal: {error}" });

        // Si hay suscripción activa, crear una nueva pendiente sin tocarla.
        // Si solo hay pendiente, reutilizarla. Si no hay ninguna, crear nueva.
        var subExistentePayPal = await _db.Suscripciones
            .Where(s => s.Estado == "pendiente" || s.Estado == "activa")
            .OrderByDescending(s => s.FechaCreacion)
            .FirstOrDefaultAsync();

        Suscripcion sub;
        if (subExistentePayPal == null || subExistentePayPal.Estado == "activa")
        {
            sub = new Suscripcion { IdPlan = plan.Id, Estado = "pendiente", FechaCreacion = DateTime.UtcNow };
            _db.Suscripciones.Add(sub);
        }
        else
        {
            sub = subExistentePayPal;
            sub.IdPlan = plan.Id;
        }

        await _db.SaveChangesAsync();

        // Registrar intento de pago — docId con prefijo PP-
        _db.PagosSuscripcion.Add(new PagoSuscripcion
        {
            IdSuscripcion           = sub.Id,
            IdPlan                  = plan.Id,
            Monto                   = plan.PrecioMensual,
            Estado                  = "pendiente",
            PagoparIdPedidoComercio = $"PP-{orderId}",
            PagoparHashPedido       = approvalUrl,
            FechaCreacion           = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("Pago PayPal iniciado: orderId={OrderId} plan={Plan}", orderId, plan.Nombre);
        return Ok(new { approvalUrl });
    }

    // ── POST /api/suscripcion/paypal-capture ─────────────────────────────────
    /// <summary>
    /// Captura la orden aprobada y activa la suscripción.
    /// Llamado por el frontend tras el retorno de PayPal (?token=ORDER_ID).
    /// </summary>
    [HttpPost("paypal-capture")]
    [Authorize]
    public async Task<IActionResult> CapturePayPal([FromBody] PayPalCaptureRequest req)
    {
        if (!User.IsInRole("Administrador")) return Forbid();

        var (success, transactionId, error) = await _paypal.CaptureOrderAsync(req.OrderId);
        if (!success)
            return BadRequest(new { message = error ?? "Error al capturar el pago con PayPal." });

        var docIdPayPal = $"PP-{req.OrderId}";
        var pago = await _db.PagosSuscripcion
            .Include(p => p.Suscripcion)
            .Include(p => p.Plan)
            .FirstOrDefaultAsync(p => p.PagoparIdPedidoComercio == docIdPayPal);

        if (pago == null)
        {
            _logger.LogWarning("paypal-capture: pago no encontrado para orderId={OrderId}", req.OrderId);
            return NotFound(new { message = "Pago no encontrado. Contactá al administrador." });
        }

        pago.Estado            = "aprobado";
        pago.FechaPago         = DateTime.UtcNow;
        pago.PagoparRespuesta  = $"paypal_capture:{transactionId}";

        if (pago.Suscripcion != null)
        {
            pago.Suscripcion.IdPlan           = pago.IdPlan;
            pago.Suscripcion.Estado           = "activa";
            pago.Suscripcion.FechaInicio      = DateTime.UtcNow;
            pago.Suscripcion.FechaVencimiento = DateTime.UtcNow.AddMonths(1);
        }

        await _db.SaveChangesAsync();

        // Enviar recibo por email (no interrumpe el flujo si falla)
        try
        {
            var admins = await _db.Usuarios
                .Where(u => u.Estado == "activo"
                         && u.UsuarioRoles.Any(ur => ur.Estado == "activo"
                                                  && ur.Rol!.NombreRol == "Administrador"))
                .Select(u => new { u.Nombre, u.Apellido, u.Email })
                .ToListAsync();

            if (admins.Count > 0)
            {
                var adminEmails = admins.Select(a => a.Email).ToList();
                var primer      = admins.First();
                var recibo = new ReciboData(
                    DocId:            docIdPayPal,
                    PlanNombre:       pago.Plan?.Nombre ?? "—",
                    Monto:            pago.Monto,
                    FechaPago:        pago.FechaPago!.Value,
                    FechaInicio:      pago.Suscripcion?.FechaInicio,
                    FechaVencimiento: pago.Suscripcion?.FechaVencimiento,
                    NombreCliente:    $"{primer.Nombre} {primer.Apellido}".Trim(),
                    EmailCliente:     primer.Email
                );
                var pdfBytes = _receipt.GenerarReciboPdf(recibo);
                await _email.EnviarReciboAsync(recibo, adminEmails, pdfBytes);
                _logger.LogInformation("Recibo PayPal enviado a {Count} admin(s)", admins.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando recibo PayPal orderId={OrderId}", req.OrderId);
        }

        _logger.LogInformation("Suscripción activada via PayPal, orderId={OrderId}", req.OrderId);
        return Ok(new { message = "¡Pago confirmado! Tu suscripción está activa." });
    }

    // ── POST /api/suscripcion/webhook/paypal ─────────────────────────────────
    /// <summary>
    /// Webhook de PayPal. Procesa PAYMENT.CAPTURE.COMPLETED y activa la suscripción.
    /// Configurar en Webhook Simulator con:
    ///   URL: https://{ngrok}/api/suscripcion/webhook/paypal
    ///   Event Type: PAYMENT.CAPTURE.COMPLETED
    /// </summary>
    [HttpPost("webhook/paypal")]
    [AllowAnonymous]
    public async Task<IActionResult> WebhookPayPal()
    {
        string body;
        using (var reader = new System.IO.StreamReader(Request.Body))
            body = await reader.ReadToEndAsync();

        _logger.LogInformation("Webhook PayPal recibido: {Body}", body);

        try
        {
            var doc       = JsonDocument.Parse(body);
            var root      = doc.RootElement;
            var eventType = root.TryGetProperty("event_type", out var et) ? et.GetString() : null;

            if (eventType != "PAYMENT.CAPTURE.COMPLETED")
            {
                _logger.LogInformation("Webhook PayPal ignorado: event_type={EventType}", eventType);
                return Ok();
            }

            // Intentar extraer orderId desde resource.supplementary_data.related_ids.order_id
            string? orderId = null;
            if (root.TryGetProperty("resource", out var resource) &&
                resource.TryGetProperty("supplementary_data", out var suppData) &&
                suppData.TryGetProperty("related_ids", out var relIds) &&
                relIds.TryGetProperty("order_id", out var oid))
            {
                orderId = oid.GetString();
            }

            // Buscar pago por orderId; si no se encuentra (ej. Webhook Simulator con ID mock),
            // usar el pago pendiente de PayPal más reciente
            PagoSuscripcion? pago = null;

            if (!string.IsNullOrEmpty(orderId))
            {
                pago = await _db.PagosSuscripcion
                    .Include(p => p.Suscripcion)
                    .Include(p => p.Plan)
                    .FirstOrDefaultAsync(p => p.PagoparIdPedidoComercio == $"PP-{orderId}");
            }

            if (pago == null)
            {
                pago = await _db.PagosSuscripcion
                    .Include(p => p.Suscripcion)
                    .Include(p => p.Plan)
                    .Where(p => p.Estado == "pendiente" && p.PagoparIdPedidoComercio!.StartsWith("PP-"))
                    .OrderByDescending(p => p.FechaCreacion)
                    .FirstOrDefaultAsync();

                if (pago == null)
                {
                    _logger.LogWarning("Webhook PayPal: no hay pagos pendientes de PayPal en la BD");
                    return Ok();
                }

                _logger.LogInformation("Webhook PayPal: fallback al pago pendiente más reciente {DocId}",
                    pago.PagoparIdPedidoComercio);
            }

            pago.Estado           = "aprobado";
            pago.FechaPago        = DateTime.UtcNow;
            pago.PagoparRespuesta = body.Length > 2000 ? body[..2000] : body;

            if (pago.Suscripcion != null)
            {
                pago.Suscripcion.IdPlan           = pago.IdPlan;
                pago.Suscripcion.Estado           = "activa";
                pago.Suscripcion.FechaInicio      = DateTime.UtcNow;
                pago.Suscripcion.FechaVencimiento = DateTime.UtcNow.AddMonths(1);
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Suscripción activada via webhook PayPal, docId={DocId}",
                pago.PagoparIdPedidoComercio);

            // Enviar recibo por email (no interrumpe el webhook si falla)
            try
            {
                var admins = await _db.Usuarios
                    .Where(u => u.Estado == "activo"
                             && u.UsuarioRoles.Any(ur => ur.Estado == "activo"
                                                      && ur.Rol!.NombreRol == "Administrador"))
                    .Select(u => new { u.Nombre, u.Apellido, u.Email })
                    .ToListAsync();

                if (admins.Count > 0)
                {
                    var adminEmails = admins.Select(a => a.Email).ToList();
                    var primer      = admins.First();
                    var recibo = new ReciboData(
                        DocId:            pago.PagoparIdPedidoComercio ?? "PP-WEBHOOK",
                        PlanNombre:       pago.Plan?.Nombre ?? "—",
                        Monto:            pago.Monto,
                        FechaPago:        pago.FechaPago!.Value,
                        FechaInicio:      pago.Suscripcion?.FechaInicio,
                        FechaVencimiento: pago.Suscripcion?.FechaVencimiento,
                        NombreCliente:    $"{primer.Nombre} {primer.Apellido}".Trim(),
                        EmailCliente:     primer.Email
                    );
                    var pdfBytes = _receipt.GenerarReciboPdf(recibo);
                    await _email.EnviarReciboAsync(recibo, adminEmails, pdfBytes);
                    _logger.LogInformation("Recibo PayPal (webhook) enviado a {Count} admin(s)", admins.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando recibo via webhook PayPal");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando webhook PayPal");
        }

        return Ok();
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────
public record IniciarPagoRequest(int IdPlan);
public record IniciarPagoPayPalRequest(int IdPlan, string FrontendUrl);
public record PayPalCaptureRequest(string OrderId, int PlanId);
public record CambiarPlanRequest(int IdPlan);
