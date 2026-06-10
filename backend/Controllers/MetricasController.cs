using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricasController : ControllerBase
{
    private readonly IMetricsCalculationService _metricsService;

    public MetricasController(IMetricsCalculationService metricsService)
    {
        _metricsService = metricsService;
    }

    [HttpGet("version/{versionId}")]
    public async Task<ActionResult<List<MetricaDto>>> GetByVersion(int versionId)
    {
        var metricas = await _metricsService.GetMetricsByVersionAsync(versionId);
        return Ok(metricas.Select(m => new MetricaDto
        {
            Id = m.Id,
            ResultadoId = m.ResultadoId,
            NombreMetrica = m.NombreMetrica,
            ValorMetrica = m.ValorMetrica,
            Unidad = m.Unidad,
            FechaCalculo = m.FechaCalculo
        }).ToList());
    }
}
