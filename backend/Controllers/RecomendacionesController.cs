using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecomendacionesController : ControllerBase
{
    private readonly IRecommendationEngine _recommendationEngine;

    public RecomendacionesController(IRecommendationEngine recommendationEngine)
    {
        _recommendationEngine = recommendationEngine;
    }

    [HttpGet("version/{versionId}")]
    public async Task<ActionResult<List<RecomendacionDto>>> GetByVersion(int versionId)
    {
        var recomendaciones = await _recommendationEngine.GetRecommendationsByVersionAsync(versionId);
        return Ok(recomendaciones.Select(r => new RecomendacionDto
        {
            Id = r.Id,
            VersionId = r.VersionId,
            TipoRecomendacion = r.TipoRecomendacion,
            Descripcion = r.Descripcion,
            Confianza = r.Confianza,
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
