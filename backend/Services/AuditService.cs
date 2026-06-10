using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;

namespace DecisionSupportAPI.Services;

public interface IAuditService
{
    Task LogActionAsync(int? usuarioId, string entidad, string accion, string? detalle = null);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;

    public AuditService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogActionAsync(int? usuarioId, string entidad, string accion, string? detalle = null)
    {
        var auditoria = new Auditoria
        {
            UsuarioId = usuarioId,
            EntidadAfectada = entidad,
            Accion = accion,
            Detalle = detalle,
            FechaEvento = DateTime.UtcNow
        };

        _context.Auditoria.Add(auditoria);
        await _context.SaveChangesAsync();
    }
}
