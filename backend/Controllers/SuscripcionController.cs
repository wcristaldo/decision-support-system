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
    private readonly IEmailService                  _email;
    private readonly IReceiptService                _receipt;
    private readonly ILogger<SuscripcionController> _logger;

    public SuscripcionController(
        ApplicationDbContext            db,
        ISuscripcionService             suscripcionService,
        IAdamsPayService                adams,
        IEmailService                   email,
        IReceiptService                 receipt,
        ILogger<SuscripcionController>  logger)
    {
        _db                 = db;
        _suscripcionService = suscripcionService;
        _adams              = adams;
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
            ? Math.Max(0, (int)(sub.FechaVencimiento.Value - DateTime.UtcNow).TotalDays)
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

        // Obtener o crear suscripción
        var sub = await _db.Suscripciones
            .Where(s => s.Estado == "pendiente" || s.Estado == "activa")
            .OrderByDescending(s => s.FechaCreacion)
            .FirstOrDefaultAsync();

        if (sub == null)
        {
            sub = new Suscripcion { IdPlan = plan.Id, Estado = "pendiente", FechaCreacion = DateTime.UtcNow };
            _db.Suscripciones.Add(sub);
        }
        else
        {
            sub.IdPlan = plan.Id;
            sub.Estado = "pendiente";
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
                    pago.Suscripcion.FechaVencimiento = DateTime.UtcNow.AddDays(30);
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
                FechaVencimiento: DateTime.UtcNow.AddDays(30)
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
}

// ── Request DTOs ──────────────────────────────────────────────────────────────
public record IniciarPagoRequest(int IdPlan);
public record CambiarPlanRequest(int IdPlan);
