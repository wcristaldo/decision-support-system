using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ver_evaluacion")]
public class RecomendacionesController : ControllerBase
{
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly ApplicationDbContext _context;

    public RecomendacionesController(IRecommendationEngine recommendationEngine, ApplicationDbContext context)
    {
        _recommendationEngine = recommendationEngine;
        _context = context;
    }

    [HttpGet("version/{versionId}")]
    public async Task<ActionResult<List<RecomendacionDto>>> GetByVersion(int versionId)
    {
        var recomendaciones = await _recommendationEngine.GetRecommendationsByVersionAsync(versionId);
        return Ok(recomendaciones.Select(r => new RecomendacionDto
        {
            Id = r.Id,
            EvaluacionId = r.EvaluacionId,
            TipoRecomendacion = r.TipoRecomendacion,
            Justificacion = r.Justificacion,
            FechaGeneracion = r.FechaGeneracion
        }).ToList());
    }

    /// <summary>
    /// GET /api/recomendaciones/resultado/{resultadoId}
    /// Devuelve las recomendaciones asociadas a un resultado de prueba específico,
    /// recorriendo la cadena: resultado → evaluaciones → recomendaciones.
    /// </summary>
    [HttpGet("resultado/{resultadoId}")]
    public async Task<ActionResult<List<RecomendacionDto>>> GetByResultado(int resultadoId)
    {
        var evaluacionIds = await _context.Evaluaciones
            .Where(e => e.ResultadoId == resultadoId)
            .Select(e => e.Id)
            .ToListAsync();

        var recomendaciones = await _context.Recomendaciones
            .Where(r => evaluacionIds.Contains(r.EvaluacionId))
            .OrderByDescending(r => r.FechaGeneracion)
            .ToListAsync();

        return Ok(recomendaciones.Select(r => new RecomendacionDto
        {
            Id = r.Id,
            EvaluacionId = r.EvaluacionId,
            TipoRecomendacion = r.TipoRecomendacion,
            Justificacion = r.Justificacion,
            FechaGeneracion = r.FechaGeneracion
        }).ToList());
    }

    /// <summary>
    /// GET /api/recomendaciones/resultado/{resultadoId}/reglas
    /// Devuelve, para cada criterio evaluado, el veredicto real que aplicó el motor
    /// de recomendación (cumple / revisar / no_cumple) contra el umbral configurado.
    /// Fuente de verdad para colorear las métricas en el frontend de forma coherente
    /// con la recomendación final, en vez de una escala de colores hardcodeada.
    /// </summary>
    [HttpGet("resultado/{resultadoId}/reglas")]
    public async Task<ActionResult<List<EvaluacionReglaDto>>> GetReglasByResultado(int resultadoId)
    {
        var evaluacionIds = await _context.Evaluaciones
            .Where(e => e.ResultadoId == resultadoId)
            .Select(e => e.Id)
            .ToListAsync();

        var reglas = await _context.EvaluacionReglas
            .Include(er => er.Regla)
            .Where(er => evaluacionIds.Contains(er.EvaluacionId))
            .ToListAsync();

        return Ok(reglas.Select(er => new EvaluacionReglaDto
        {
            Criterio       = er.Regla?.Criterio,
            ResultadoRegla = er.ResultadoRegla,
            Observacion    = er.Observacion
        }).ToList());
    }

    [HttpPost("generar/{versionId}")]
    [Authorize(Policy = "ejecutar_evaluacion")]
    public async Task<IActionResult> Generate(int versionId)
    {
        await _recommendationEngine.GenerateRecommendationsAsync(versionId);
        return Ok(new { message = "Recomendaciones generadas exitosamente" });
    }
}
