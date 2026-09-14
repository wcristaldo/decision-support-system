using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace DecisionSupportAPI.Services;

public interface IAuditoriaService
{
    /// <summary>
    /// usuarioIdExplicito: usar cuando la request no está autenticada al momento
    /// de auditar (ej. Login: el JWT recién se emite como resultado de esta
    /// llamada, así que HttpContext.User todavía no tiene claims). Si se omite,
    /// el usuario se toma del claim NameIdentifier de la request actual.
    /// </summary>
    Task RegistrarAsync(string accion, string entidad, int? idRegistro, string? detalle, int? usuarioIdExplicito = null);
}

public class AuditoriaService : IAuditoriaService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditoriaService> _logger;

    // Debe coincidir con auditoria.detalle VARCHAR(255) — truncar en vez de
    // dejar que el INSERT falle y el evento se pierda sin dejar rastro.
    private const int MaxLargoDetalle = 255;

    public AuditoriaService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<AuditoriaService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task RegistrarAsync(string accion, string entidad, int? idRegistro, string? detalle, int? usuarioIdExplicito = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return;

            int? usuarioId = usuarioIdExplicito;
            if (usuarioId == null)
            {
                var usuarioIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? httpContext.User.FindFirst("sub");
                if (usuarioIdClaim != null && int.TryParse(usuarioIdClaim.Value, out var id))
                {
                    usuarioId = id;
                }
            }

            var ipOrigen = httpContext.Connection.RemoteIpAddress?.ToString();

            var auditoria = new Auditoria
            {
                UsuarioId = usuarioId,
                Accion = accion,
                EntidadAfectada = entidad,
                IdRegistroAfectado = idRegistro,
                Detalle = detalle != null && detalle.Length > MaxLargoDetalle
                    ? detalle[..MaxLargoDetalle]
                    : detalle,
                FechaEvento = DateTime.UtcNow,
                IpOrigen = ipOrigen
            };

            _context.Auditoria.Add(auditoria);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // No relanzar: un fallo de auditoría no debe interrumpir la acción
            // real del usuario, pero debe quedar visible en logs — antes este
            // catch era completamente silencioso y podía perder eventos de
            // auditoría (la propia evidencia de trazabilidad de RF14) sin dejar rastro.
            _logger.LogError(ex, "No se pudo registrar auditoría: accion={Accion}, entidad={Entidad}", accion, entidad);
        }
    }
}
