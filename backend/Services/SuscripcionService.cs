using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DecisionSupportAPI.Services;

// ── Resultado de verificación de límite ───────────────────────────────────

public record LimiteVerificacion(bool Permitido, string? Mensaje = null);

// ── Interfaz ───────────────────────────────────────────────────────────────

public interface ISuscripcionService
{
    /// <summary>Retorna la suscripción activa del sistema, o null si no hay.</summary>
    Task<Suscripcion?> GetSuscripcionActivaAsync();

    /// <summary>Retorna el plan activo actual, o null si no hay suscripción activa.</summary>
    Task<PlanSuscripcion?> GetPlanActivoAsync();

    /// <summary>Verifica si se puede crear un nuevo proyecto según los límites del plan.</summary>
    Task<LimiteVerificacion> VerificarLimiteProyectosAsync();

    /// <summary>Verifica si se puede crear un nuevo usuario según los límites del plan.</summary>
    Task<LimiteVerificacion> VerificarLimiteUsuariosAsync();

    /// <summary>Verifica si se puede cargar un nuevo resultado de prueba este mes.</summary>
    Task<LimiteVerificacion> VerificarLimiteEvaluacionesMesAsync();

    /// <summary>Verifica el tamaño de archivo permitido (en bytes).</summary>
    Task<LimiteVerificacion> VerificarTamanoArchivoAsync(long tamanoBytes);
}

// ── Implementación ─────────────────────────────────────────────────────────

public class SuscripcionService : ISuscripcionService
{
    private readonly ApplicationDbContext _db;

    public SuscripcionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Suscripcion?> GetSuscripcionActivaAsync()
        => await _db.Suscripciones
            .Include(s => s.Plan)
            .Where(s => s.Estado == "activa"
                     && (s.FechaVencimiento == null || s.FechaVencimiento > DateTime.UtcNow))
            .OrderByDescending(s => s.FechaCreacion)
            .FirstOrDefaultAsync();

    public async Task<PlanSuscripcion?> GetPlanActivoAsync()
    {
        var sub = await GetSuscripcionActivaAsync();
        return sub?.Plan;
    }

    public async Task<LimiteVerificacion> VerificarLimiteProyectosAsync()
    {
        var plan = await GetPlanActivoAsync();
        if (plan == null)
            return new(false, "No hay suscripción activa. Contratá un plan para continuar.");

        if (plan.MaxProyectos == null)   // ilimitado
            return new(true);

        var actual = await _db.Proyectos
            .CountAsync(p => p.Estado == "activo");

        if (actual >= plan.MaxProyectos)
            return new(false, $"Tu plan {plan.Nombre} permite hasta {plan.MaxProyectos} proyecto(s) activo(s). Actualizá tu plan para continuar.");

        return new(true);
    }

    public async Task<LimiteVerificacion> VerificarLimiteUsuariosAsync()
    {
        var plan = await GetPlanActivoAsync();
        if (plan == null)
            return new(false, "No hay suscripción activa. Contratá un plan para continuar.");

        if (plan.MaxUsuarios == null)    // ilimitado
            return new(true);

        var actual = await _db.Usuarios
            .CountAsync(u => u.Estado == "activo");

        if (actual >= plan.MaxUsuarios)
            return new(false, $"Tu plan {plan.Nombre} permite hasta {plan.MaxUsuarios} usuario(s) activo(s). Actualizá tu plan para continuar.");

        return new(true);
    }

    public async Task<LimiteVerificacion> VerificarLimiteEvaluacionesMesAsync()
    {
        var plan = await GetPlanActivoAsync();
        if (plan == null)
            return new(false, "No hay suscripción activa. Contratá un plan para continuar.");

        if (plan.MaxEvaluacionesMes == null)   // ilimitado
            return new(true);

        var inicioMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var actual = await _db.ResultadosPrueba
            .CountAsync(r => r.FechaCarga >= inicioMes);

        if (actual >= plan.MaxEvaluacionesMes)
            return new(false, $"Tu plan {plan.Nombre} permite hasta {plan.MaxEvaluacionesMes} evaluaciones/mes. Actualizá tu plan para continuar.");

        return new(true);
    }

    public async Task<LimiteVerificacion> VerificarTamanoArchivoAsync(long tamanoBytes)
    {
        var plan = await GetPlanActivoAsync();
        if (plan == null)
            return new(false, "No hay suscripción activa. Contratá un plan para continuar.");

        if (plan.MaxTamanoArchivoMb == null)   // ilimitado
            return new(true);

        var maxBytes = (long)plan.MaxTamanoArchivoMb * 1024 * 1024;
        if (tamanoBytes > maxBytes)
            return new(false, $"Tu plan {plan.Nombre} permite archivos de hasta {plan.MaxTamanoArchivoMb} MB. El archivo supera ese límite.");

        return new(true);
    }
}
