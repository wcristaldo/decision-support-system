using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
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

    [HttpPost("generar/{versionId}")]
    public async Task<IActionResult> Generate(int versionId)
    {
        await _recommendationEngine.GenerateRecommendationsAsync(versionId);
        return Ok(new { message = "Recomendaciones generadas exitosamente" });
    }
}
