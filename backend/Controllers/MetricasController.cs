using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ver_resultados")]
public class MetricasController : ControllerBase
{
    private readonly IMetricsCalculationService _metricsService;
    private readonly IProyectoAccesoService _acceso;

    public MetricasController(IMetricsCalculationService metricsService, IProyectoAccesoService acceso)
    {
        _metricsService = metricsService;
        _acceso = acceso;
    }

    [HttpGet("version/{versionId}")]
    public async Task<ActionResult<List<MetricaDto>>> GetByVersion(int versionId)
    {
        if (!await _acceso.TieneAccesoAVersionAsync(User, versionId)) return Forbid();

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

    [HttpGet("resultado/{resultadoId}")]
    public async Task<ActionResult<List<MetricaDto>>> GetByResultado(int resultadoId)
    {
        if (!await _acceso.TieneAccesoAResultadoAsync(User, resultadoId)) return Forbid();

        var metricas = await _metricsService.GetMetricsByResultadoAsync(resultadoId);
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
