using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalisisController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ISuscripcionService _suscripcionService;

    public AnalisisController(ApplicationDbContext context, ISuscripcionService suscripcionService)
    {
        _context = context;
        _suscripcionService = suscripcionService;
    }

    /// <summary>
    /// GET /api/analisis/historial
    /// Devuelve todas las versiones que tienen al menos una recomendación generada,
    /// con nombre de proyecto, métricas clave y tipo de recomendación.
    /// Ordenado por fecha de evaluación descendente.
    /// </summary>
    [HttpGet("historial")]
    public async Task<ActionResult<List<AnalisisHistorialDto>>> GetHistorial()
    {
        // Aplicar límite de historial según plan activo
        var plan = await _suscripcionService.GetPlanActivoAsync();
        DateTime? fechaMinima = plan?.HistorialDias.HasValue == true
            ? DateTime.UtcNow.AddDays(-plan.HistorialDias!.Value)
            : null;

        // Carga recomendaciones con la cadena completa: evaluacion → resultado → version + metricas
        var query = _context.Recomendaciones
            .Include(r => r.Evaluacion)
                .ThenInclude(e => e!.Resultado)
                    .ThenInclude(rp => rp!.Version)
            .Include(r => r.Evaluacion)
                .ThenInclude(e => e!.Resultado)
                    .ThenInclude(rp => rp!.Metricas)
            .AsQueryable();

        if (fechaMinima.HasValue)
            query = query.Where(r => r.FechaGeneracion >= fechaMinima.Value);

        var recomendaciones = await query
            .OrderByDescending(r => r.FechaGeneracion)
            .ToListAsync();

        // Obtener proyectos en una sola consulta para evitar N+1
        var proyectoIds = recomendaciones
            .Select(r => r.Evaluacion?.Resultado?.Version?.ProyectoId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var proyectos = await _context.Proyectos
            .Where(p => proyectoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var resultado = recomendaciones
            .Where(r =>
                r.Evaluacion?.Resultado?.Version != null)
            .Select(r =>
            {
                var rp      = r.Evaluacion!.Resultado!;
                var version = rp.Version!;
                proyectos.TryGetValue(version.ProyectoId, out var proyecto);

                var metricas = rp.Metricas;

                decimal? GetMetrica(string nombre) =>
                    metricas.FirstOrDefault(m => m.NombreMetrica == nombre)?.ValorMetrica;

                return new AnalisisHistorialDto
                {
                    ProyectoId       = version.ProyectoId,
                    ProyectoNombre   = proyecto?.Nombre ?? "—",
                    ProyectoTipo     = proyecto?.TipoSolucion ?? "",
                    VersionId        = version.Id,
                    VersionNumero    = version.NumeroVersion,
                    ResultadoId      = rp.Id,
                    RecomendacionId  = r.Id,
                    Recomendacion    = r.TipoRecomendacion ?? "",
                    Justificacion    = r.Justificacion,
                    FechaCarga       = rp.FechaCarga,
                    FechaEvaluacion  = r.Evaluacion!.FechaEvaluacion,
                    TasaExito        = GetMetrica("tasa_exito"),
                    TasaFallo        = GetMetrica("tasa_fallo"),
                    Cobertura        = GetMetrica("cobertura"),
                    TiempoEjecucion  = GetMetrica("tiempo_ejecucion"),
                    TotalPruebas     = GetMetrica("total_pruebas"),
                    PruebasExitosas  = GetMetrica("pruebas_exitosas"),
                    PruebasFallidas  = GetMetrica("pruebas_fallidas"),
                };
            })
            .ToList();

        return Ok(resultado);
    }
}
