using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResultadosPruebaController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMetricsCalculationService _metricsService;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly IAuditService _auditService;

    public ResultadosPruebaController(
        ApplicationDbContext context,
        IMetricsCalculationService metricsService,
        IRecommendationEngine recommendationEngine,
        IAuditService auditService)
    {
        _context = context;
        _metricsService = metricsService;
        _recommendationEngine = recommendationEngine;
        _auditService = auditService;
    }

    [HttpGet("version/{versionId}")]
    public async Task<ActionResult<List<ResultadoPruebaDto>>> GetByVersion(int versionId)
    {
        var resultados = await _context.ResultadosPrueba
            .Where(r => r.VersionId == versionId)
            .ToListAsync();

        return Ok(resultados.Select(r => new ResultadoPruebaDto
        {
            Id = r.Id,
            VersionId = r.VersionId,
            NombrePrueba = r.NombrePrueba,
            TipoPrueba = r.TipoPrueba,
            Estado = r.Estado,
            TiempoEjecucion = r.TiempoEjecucion,
            Mensaje = r.Mensaje,
            FechaEjecucion = r.FechaEjecucion
        }).ToList());
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadResults([FromBody] UploadResultadosPruebaDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var version = await _context.Versiones.FirstOrDefaultAsync(v => v.Id == request.VersionId);
        if (version == null)
            return NotFound("Versión no encontrada");

        // Limpiar resultados anteriores
        var resultadosAnteriores = await _context.ResultadosPrueba
            .Where(r => r.VersionId == request.VersionId)
            .ToListAsync();
        _context.ResultadosPrueba.RemoveRange(resultadosAnteriores);

        // Agregar nuevos resultados
        var resultados = request.Resultados.Select(r => new ResultadoPrueba
        {
            VersionId = request.VersionId,
            NombrePrueba = r.NombrePrueba,
            TipoPrueba = r.TipoPrueba,
            Estado = r.Estado,
            TiempoEjecucion = r.TiempoEjecucion,
            Mensaje = r.Mensaje
        }).ToList();

        _context.ResultadosPrueba.AddRange(resultados);
        await _context.SaveChangesAsync();

        // Calcular métricas
        await _metricsService.CalculateMetricsAsync(request.VersionId);

        // Generar recomendaciones
        await _recommendationEngine.GenerateRecommendationsAsync(request.VersionId);

        // Auditoría
        await _auditService.LogActionAsync(null, "ResultadosPrueba", "CARGAR", null, $"VersionId: {request.VersionId}, Cantidad: {resultados.Count}");

        return Ok(new { message = "Resultados cargados exitosamente", cantidad = resultados.Count });
    }

    [HttpPost]
    public async Task<ActionResult<ResultadoPruebaDto>> Create([FromBody] CreateResultadoPruebaDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var resultado = new ResultadoPrueba
        {
            VersionId = request.VersionId,
            NombrePrueba = request.NombrePrueba,
            TipoPrueba = request.TipoPrueba,
            Estado = request.Estado,
            TiempoEjecucion = request.TiempoEjecucion,
            Mensaje = request.Mensaje
        };

        _context.ResultadosPrueba.Add(resultado);
        await _context.SaveChangesAsync();

        // Recalcular métricas
        await _metricsService.CalculateMetricsAsync(request.VersionId);
        await _recommendationEngine.GenerateRecommendationsAsync(request.VersionId);

        return CreatedAtAction(nameof(GetByVersion), new { versionId = request.VersionId }, new ResultadoPruebaDto
        {
            Id = resultado.Id,
            VersionId = resultado.VersionId,
            NombrePrueba = resultado.NombrePrueba,
            TipoPrueba = resultado.TipoPrueba,
            Estado = resultado.Estado,
            TiempoEjecucion = resultado.TiempoEjecucion,
            Mensaje = resultado.Mensaje,
            FechaEjecucion = resultado.FechaEjecucion
        });
    }
}
