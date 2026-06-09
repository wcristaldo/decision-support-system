using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;

namespace DecisionSupportAPI.Services;

public interface IAuditService
{
    Task LogActionAsync(int? usuarioId, string entidad, string accion, string? datosAnteriores = null, string? datosNuevos = null);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;

    public AuditService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogActionAsync(int? usuarioId, string entidad, string accion, string? datosAnteriores = null, string? datosNuevos = null)
    {
        var auditoria = new Auditoria
        {
            UsuarioId = usuarioId,
            Entidad = entidad,
            Accion = accion,
            DatosAnteriores = datosAnteriores,
            DatosNuevos = datosNuevos,
            FechaEvento = DateTime.UtcNow
        };

        _context.Auditoria.Add(auditoria);
        await _context.SaveChangesAsync();
    }
}
