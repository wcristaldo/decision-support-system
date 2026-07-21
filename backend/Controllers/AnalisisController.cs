using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalisisController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AnalisisController(ApplicationDbContext context)
    {
        _context = context;
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
        // Carga recomendaciones con la cadena completa: evaluacion → resultado → version + metricas
        var recomendaciones = await _context.Recomendaciones
            .Include(r => r.Evaluacion)
                .ThenInclude(e => e!.Resultado)
                    .ThenInclude(rp => rp!.Version)
            .Include(r => r.Evaluacion)
                .ThenInclude(e => e!.Resultado)
                    .ThenInclude(rp => rp!.Metricas)
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
