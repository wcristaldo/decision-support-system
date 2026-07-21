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
    private readonly ApplicationDbContext  _db;
    private readonly ISuscripcionService  _suscripcionService;
    private readonly IPagoparService      _pagopar;
    private readonly IConfiguration       _config;
    private readonly ILogger<SuscripcionController> _logger;

    public SuscripcionController(
        ApplicationDbContext db,
        ISuscripcionService suscripcionService,
        IPagoparService pagopar,
        IConfiguration config,
        ILogger<SuscripcionController> logger)
    {
        _db                 = db;
        _suscripcionService = suscripcionService;
        _pagopar            = pagopar;
        _config             = config;
        _logger             = logger;
    }

    // ── GET /api/suscripcion/planes ──────────────────────────────────────
    /// <summary>Lista todos los planes disponibles con sus límites y funcionalidades.</summary>
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
                    MaxProyectos        = p.MaxProyectos        == null ? "Ilimitado" : p.MaxProyectos.ToString(),
                    MaxUsuarios         = p.MaxUsuarios         == null ? "Ilimitado" : p.MaxUsuarios.ToString(),
                    MaxEvaluacionesMes  = p.MaxEvaluacionesMes  == null ? "Ilimitadas" : p.MaxEvaluacionesMes.ToString(),
                    MaxTamanoArchivoMb  = p.MaxTamanoArchivoMb,
                    HistorialDias       = p.HistorialDias        == null ? "Completo" : $"Últimos {p.HistorialDias} días",
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
    /// <summary>Retorna la suscripción activa con el plan y uso actual.</summary>
    [HttpGet("actual")]
    [Authorize]
    public async Task<IActionResult> GetActual()
    {
        var sub = await _suscripcionService.GetSuscripcionActivaAsync();

        if (sub == null)
            return Ok(new { activa = false, mensaje = "Sin suscripción activa." });

        // Uso actual para mostrar barra de progreso
        var inicioMes   = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var proyectos   = await _db.Proyectos.CountAsync(p => p.Estado == "activo");
        var usuarios    = await _db.Usuarios.CountAsync(u => u.Estado == "activo");
        var evalMes     = await _db.ResultadosPrueba.CountAsync(r => r.FechaCarga >= inicioMes);

        return Ok(new
        {
            activa          = true,
            id              = sub.Id,
            plan            = new { sub.Plan!.Id, sub.Plan.Nombre, sub.Plan.PrecioMensual },
            estado          = sub.Estado,
            fechaInicio     = sub.FechaInicio,
            fechaVencimiento = sub.FechaVencimiento,
            tieneTarjeta    = sub.PagoparIdTarjeta != null,
            usoActual = new
            {
                proyectos,
                maxProyectos        = sub.Plan.MaxProyectos,
                usuarios,
                maxUsuarios         = sub.Plan.MaxUsuarios,
                evaluacionesMes     = evalMes,
                maxEvaluacionesMes  = sub.Plan.MaxEvaluacionesMes,
            }
        });
    }

    // ── POST /api/suscripcion/iniciar-pago ───────────────────────────────
    /// <summary>
    /// Administrador: inicia el checkout de Pagopar para contratar/renovar un plan.
    /// Crea el pedido en Pagopar y retorna la URL de checkout.
    /// </summary>
    [HttpPost("iniciar-pago")]
    [Authorize]
    public async Task<IActionResult> IniciarPago([FromBody] IniciarPagoRequest req)
    {
        if (!User.IsInRole("Administrador"))
            return Forbid();

        var plan = await _db.PlanesSuscripcion.FindAsync(req.IdPlan);
        if (plan == null)
            return NotFound(new { message = "Plan no encontrado." });

        // Generar ID único de pedido
        var idPedidoComercio = $"SAD-{DateTime.UtcNow:yyyyMMddHHmmss}-{plan.Id}";
        var fechaMaxima = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss");

        var resultado = await _pagopar.IniciarTransaccionAsync(new(
            IdPedidoComercio:   idPedidoComercio,
            MontoTotal:         plan.PrecioMensual,
            NombreComprador:    req.NombreComprador,
            EmailComprador:     req.EmailComprador,
            DocumentoComprador: req.DocumentoComprador,
            DescripcionProducto: $"SAD-Roshka Plan {plan.Nombre} - {DateTime.UtcNow:MM/yyyy}",
            IdProducto:         plan.Id,
            FechaMaximaPago:    fechaMaxima
        ));

        if (!resultado.Respuesta)
            return BadRequest(new { message = $"Error al crear pedido en Pagopar: {resultado.Error}" });

        // Crear o actualizar suscripción en estado pendiente
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

        // Registrar el pago pendiente
        _db.PagosSuscripcion.Add(new PagoSuscripcion
        {
            IdSuscripcion           = sub.Id,
            IdPlan                  = plan.Id,
            Monto                   = plan.PrecioMensual,
            Estado                  = "pendiente",
            PagoparIdPedidoComercio = idPedidoComercio,
            PagoparHashPedido       = resultado.HashPedido,
            PagoparNumeroPedido     = resultado.NumeroPedido,
            FechaCreacion           = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            hashPedido  = resultado.HashPedido,
            numeroPedido = resultado.NumeroPedido,
            checkoutUrl = _pagopar.GetCheckoutUrl(resultado.HashPedido!),
            mensaje     = $"Pedido creado. Redirigí al usuario a checkoutUrl para completar el pago."
        });
    }

    // ── POST /api/suscripcion/webhook ────────────────────────────────────
    /// <summary>
    /// Endpoint de notificación de Pagopar (configurar en panel Pagopar).
    /// Valida el token y activa/revierte la suscripción según el estado del pago.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        string body;
        using (var reader = new System.IO.StreamReader(Request.Body))
            body = await reader.ReadToEndAsync();

        _logger.LogInformation("Webhook Pagopar recibido: {Body}", body);

        try
        {
            var doc    = JsonDocument.Parse(body);
            var root   = doc.RootElement;
            var items  = root.GetProperty("resultado");
            var item   = items[0];

            var hashPedido      = item.GetProperty("hash_pedido").GetString()!;
            var tokenRecibido   = item.GetProperty("token").GetString()!;
            var pagado          = item.GetProperty("pagado").GetBoolean();
            var cancelado       = item.GetProperty("cancelado").GetBoolean();
            var monto           = item.GetProperty("monto").GetString();

            // Validar token — CRÍTICO para evitar fraude
            if (!_pagopar.ValidarTokenWebhook(hashPedido, tokenRecibido))
            {
                _logger.LogWarning("Webhook Pagopar con token inválido. Hash: {Hash}", hashPedido);
                return BadRequest("Token no válido");
            }

            // Buscar el pago correspondiente
            var pago = await _db.PagosSuscripcion
                .Include(p => p.Suscripcion)
                .FirstOrDefaultAsync(p => p.PagoparHashPedido == hashPedido);

            if (pago == null)
            {
                _logger.LogWarning("Webhook: no se encontró pago con hash {Hash}", hashPedido);
                return Ok(items.GetRawText()); // Pagopar requiere 200
            }

            pago.PagoparRespuesta = body;

            if (pagado && !cancelado)
            {
                pago.Estado     = "aprobado";
                pago.FechaPago  = DateTime.UtcNow;

                // Activar suscripción por 30 días
                if (pago.Suscripcion != null)
                {
                    pago.Suscripcion.Estado           = "activa";
                    pago.Suscripcion.FechaInicio      = DateTime.UtcNow;
                    pago.Suscripcion.FechaVencimiento = DateTime.UtcNow.AddDays(30);
                }

                _logger.LogInformation("Suscripción activada tras pago del pedido {Hash}", hashPedido);
            }
            else
            {
                pago.Estado = "revertido";

                if (pago.Suscripcion != null && pago.Suscripcion.Estado == "activa")
                    pago.Suscripcion.Estado = "vencida";

                _logger.LogInformation("Pago revertido/cancelado para pedido {Hash}", hashPedido);
            }

            await _db.SaveChangesAsync();

            // Pagopar requiere que el comercio retorne el mismo JSON de resultado
            return Ok(items.GetRawText());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando webhook Pagopar");
            return StatusCode(500);
        }
    }

    // ── POST /api/suscripcion/recurrente/registrar-cliente ───────────────
    /// <summary>Registra al usuario administrador como cliente en Pagopar para pagos recurrentes.</summary>
    [HttpPost("recurrente/registrar-cliente")]
    [Authorize]
    public async Task<IActionResult> RegistrarCliente([FromBody] RegistrarClienteRequest req)
    {
        if (!User.IsInRole("Administrador"))
            return Forbid();

        var resultado = await _pagopar.AgregarClienteAsync(
            identificador:   req.UsuarioId,
            nombreApellido:  req.NombreApellido,
            email:           req.Email,
            celular:         req.Celular
        );

        if (!resultado.Respuesta)
            return BadRequest(new { message = $"Error en Pagopar: {resultado.Error}" });

        // Guardar el identificador en la suscripción
        var sub = await _suscripcionService.GetSuscripcionActivaAsync();
        if (sub != null)
        {
            sub.PagoparIdCliente = req.UsuarioId;
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "Cliente registrado en Pagopar.", resultado = resultado.Resultado });
    }

    // ── POST /api/suscripcion/recurrente/agregar-tarjeta ─────────────────
    /// <summary>Inicia el catastro de tarjeta. Retorna el token para mostrar el iframe.</summary>
    [HttpPost("recurrente/agregar-tarjeta")]
    [Authorize]
    public async Task<IActionResult> AgregarTarjeta([FromBody] AgregarTarjetaRequest req)
    {
        if (!User.IsInRole("Administrador"))
            return Forbid();

        var frontendUrl = _config["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var urlRetorno  = $"{frontendUrl}/suscripcion?accion=tarjeta-resultado";

        var resultado = await _pagopar.AgregarTarjetaAsync(
            identificador: req.UsuarioId,
            urlRetorno:    urlRetorno,
            proveedor:     req.Proveedor ?? "uPay"
        );

        if (!resultado.Respuesta)
            return BadRequest(new { message = $"Error en Pagopar: {resultado.Error}" });

        // El resultado es el token para construir el iframe
        var iframeUrl = $"https://www.pagopar.com/upay-iframe/?id-form={resultado.Resultado}";

        return Ok(new
        {
            tokenFormulario = resultado.Resultado,
            iframeUrl,
            mensaje = "Mostrá el iframeUrl en el frontend para que el usuario ingrese su tarjeta."
        });
    }

    // ── POST /api/suscripcion/recurrente/confirmar-tarjeta ───────────────
    /// <summary>Confirma el catastro de tarjeta (obligatorio tras el iframe).</summary>
    [HttpPost("recurrente/confirmar-tarjeta")]
    [Authorize]
    public async Task<IActionResult> ConfirmarTarjeta([FromBody] ConfirmarTarjetaRequest req)
    {
        if (!User.IsInRole("Administrador"))
            return Forbid();

        var frontendUrl = _config["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var urlRetorno  = $"{frontendUrl}/suscripcion";

        var resultado = await _pagopar.ConfirmarTarjetaAsync(req.UsuarioId, urlRetorno);

        if (!resultado.Respuesta)
            return BadRequest(new { message = $"Error al confirmar tarjeta: {resultado.Error}" });

        return Ok(new { message = "Tarjeta confirmada correctamente." });
    }

    // ── GET /api/suscripcion/recurrente/tarjetas/{usuarioId} ─────────────
    /// <summary>Lista las tarjetas catastradas de un usuario.</summary>
    [HttpGet("recurrente/tarjetas/{usuarioId}")]
    [Authorize]
    public async Task<IActionResult> ListarTarjetas(int usuarioId)
    {
        if (!User.IsInRole("Administrador"))
            return Forbid();

        var resultado = await _pagopar.ListarTarjetasAsync(usuarioId);

        if (!resultado.Respuesta)
            return BadRequest(new { message = $"Error: {resultado.Error}" });

        return Ok(new
        {
            tarjetas = resultado.Tarjetas?.Select(t => new
            {
                t.Tarjeta,
                t.TarjetaNumero,
                t.Marca,
                t.Emisor,
                t.TipoTarjeta,
                t.Proveedor,
                t.UrlLogo,
                // No retornar alias_token al frontend por seguridad
            })
        });
    }

    // ── POST /api/suscripcion/recurrente/cobrar ───────────────────────────
    /// <summary>
    /// Debita el plan activo con la tarjeta catastrada.
    /// Primero crea el pedido en Pagopar, luego debita recurrentemente.
    /// </summary>
    [HttpPost("recurrente/cobrar")]
    [Authorize]
    public async Task<IActionResult> CobrarRecurrente([FromBody] CobrarRecurrenteRequest req)
    {
        if (!User.IsInRole("Administrador"))
            return Forbid();

        var sub = await _suscripcionService.GetSuscripcionActivaAsync();
        if (sub == null)
            return BadRequest(new { message = "No hay suscripción activa." });

        if (sub.PagoparIdCliente == null)
            return BadRequest(new { message = "No hay cliente registrado en Pagopar. Registrá el cliente primero." });

        // 1. Listar tarjetas para obtener alias_token actual
        var listado = await _pagopar.ListarTarjetasAsync(sub.PagoparIdCliente.Value);
        if (!listado.Respuesta || listado.Tarjetas == null || listado.Tarjetas.Count == 0)
            return BadRequest(new { message = "No hay tarjetas catastradas. Agregá una tarjeta primero." });

        var tarjeta = listado.Tarjetas.First(); // usar la primera tarjeta
        var aliasToken = tarjeta.AliasToken;

        // 2. Crear pedido en Pagopar
        var idPedidoComercio = $"SAD-REC-{DateTime.UtcNow:yyyyMMddHHmmss}-{sub.IdPlan}";
        var txResult = await _pagopar.IniciarTransaccionAsync(new(
            IdPedidoComercio:   idPedidoComercio,
            MontoTotal:         sub.Plan!.PrecioMensual,
            NombreComprador:    req.NombreComprador,
            EmailComprador:     req.EmailComprador,
            DocumentoComprador: req.DocumentoComprador,
            DescripcionProducto: $"Renovación SAD-Roshka Plan {sub.Plan.Nombre} - {DateTime.UtcNow:MM/yyyy}",
            IdProducto:         sub.IdPlan,
            FechaMaximaPago:    DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss")
        ));

        if (!txResult.Respuesta)
            return BadRequest(new { message = $"Error al crear pedido: {txResult.Error}" });

        // 3. Debitar con la tarjeta catastrada
        var pagoResult = await _pagopar.PagarRecurrenteAsync(txResult.HashPedido!, aliasToken, sub.PagoparIdCliente.Value);

        // Registrar intento de pago
        var pago = new PagoSuscripcion
        {
            IdSuscripcion           = sub.Id,
            IdPlan                  = sub.IdPlan,
            Monto                   = sub.Plan.PrecioMensual,
            Estado                  = pagoResult.Respuesta ? "aprobado" : "rechazado",
            PagoparIdPedidoComercio = idPedidoComercio,
            PagoparHashPedido       = txResult.HashPedido,
            PagoparNumeroPedido     = txResult.NumeroPedido,
            PagoparRespuesta        = pagoResult.Resultado,
            FechaPago               = pagoResult.Respuesta ? DateTime.UtcNow : null,
            FechaCreacion           = DateTime.UtcNow
        };

        _db.PagosSuscripcion.Add(pago);

        if (pagoResult.Respuesta)
        {
            sub.Estado           = "activa";
            sub.FechaInicio      = DateTime.UtcNow;
            sub.FechaVencimiento = DateTime.UtcNow.AddDays(30);
        }

        await _db.SaveChangesAsync();

        if (!pagoResult.Respuesta)
            return BadRequest(new { message = $"El cobro fue rechazado: {pagoResult.Error}" });

        return Ok(new { message = "Cobro realizado correctamente. Suscripción renovada por 30 días." });
    }

    // ── GET /api/suscripcion/pagos ────────────────────────────────────────
    /// <summary>Historial de pagos de suscripción.</summary>
    [HttpGet("pagos")]
    [Authorize]
    public async Task<IActionResult> GetPagos()
    {
        if (!User.IsInRole("Administrador"))
            return Forbid();

        var pagos = await _db.PagosSuscripcion
            .Include(p => p.Plan)
            .OrderByDescending(p => p.FechaCreacion)
            .Select(p => new
            {
                p.Id,
                p.Monto,
                p.Estado,
                Plan     = p.Plan!.Nombre,
                p.PagoparIdPedidoComercio,
                p.PagoparNumeroPedido,
                p.FechaPago,
                p.FechaCreacion
            })
            .ToListAsync();

        return Ok(pagos);
    }

    // ── POST /api/suscripcion/cambiar-plan ────────────────────────────────
    /// <summary>Cambia el plan de la suscripción activa (downgrade/upgrade).</summary>
    [HttpPost("cambiar-plan")]
    [Authorize]
    public async Task<IActionResult> CambiarPlan([FromBody] CambiarPlanRequest req)
    {
        if (!User.IsInRole("Administrador"))
            return Forbid();

        var plan = await _db.PlanesSuscripcion.FindAsync(req.IdPlan);
        if (plan == null)
            return NotFound(new { message = "Plan no encontrado." });

        var sub = await _suscripcionService.GetSuscripcionActivaAsync();
        if (sub == null)
            return BadRequest(new { message = "No hay suscripción activa para cambiar." });

        sub.IdPlan = plan.Id;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Plan cambiado a {plan.Nombre}. El nuevo plan aplica de inmediato." });
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────

public record IniciarPagoRequest(
    int    IdPlan,
    string NombreComprador,
    string EmailComprador,
    string DocumentoComprador
);

public record RegistrarClienteRequest(
    int    UsuarioId,
    string NombreApellido,
    string Email,
    string Celular
);

public record AgregarTarjetaRequest(
    int     UsuarioId,
    string? Proveedor
);

public record ConfirmarTarjetaRequest(int UsuarioId);

public record CobrarRecurrenteRequest(
    string NombreComprador,
    string EmailComprador,
    string DocumentoComprador
);

public record CambiarPlanRequest(int IdPlan);
