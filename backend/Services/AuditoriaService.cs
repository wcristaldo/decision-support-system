using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace DecisionSupportAPI.Services;

public interface IAuditoriaService
{
    Task RegistrarAsync(string accion, string entidad, int? idRegistro, string? detalle);
}

public class AuditoriaService : IAuditoriaService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditoriaService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task RegistrarAsync(string accion, string entidad, int? idRegistro, string? detalle)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return;

            var usuarioIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirst("sub");

            int? usuarioId = null;
            if (usuarioIdClaim != null && int.TryParse(usuarioIdClaim.Value, out var id))
            {
                usuarioId = id;
            }

            var ipOrigen = httpContext.Connection.RemoteIpAddress?.ToString();

            var auditoria = new Auditoria
            {
                UsuarioId = usuarioId,
                Accion = accion,
                EntidadAfectada = entidad,
                IdRegistroAfectado = idRegistro,
                Detalle = detalle,
                FechaEvento = DateTime.UtcNow,
                IpOrigen = ipOrigen
            };

            _context.Auditoria.Add(auditoria);
            await _context.SaveChangesAsync();
        }
        catch
        {
            // No lanzar excepción si falla el registro de auditoría
            // Solo registrar en logs si es necesario
        }
    }
}
