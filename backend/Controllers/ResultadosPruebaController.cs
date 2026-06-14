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
            UsuarioCargaId = r.UsuarioCargaId,
            NombreArchivo = r.NombreArchivo,
            FormatoArchivo = r.FormatoArchivo,
            RutaArchivo = r.RutaArchivo,
            FechaCarga = r.FechaCarga,
            EstadoValidacion = r.EstadoValidacion,
            Observaciones = r.Observaciones
        }).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ResultadoPruebaDto>> Create([FromBody] CreateResultadoPruebaDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var version = await _context.Versiones.FirstOrDefaultAsync(v => v.Id == request.VersionId);
        if (version == null)
            return NotFound("Versión no encontrada");

        var resultado = new ResultadoPrueba
        {
            VersionId = request.VersionId,
            NombreArchivo = request.NombreArchivo,
            FormatoArchivo = request.FormatoArchivo,
            RutaArchivo = request.RutaArchivo,
            Observaciones = request.Observaciones,
            EstadoValidacion = "pendiente"
        };

        _context.ResultadosPrueba.Add(resultado);
        await _context.SaveChangesAsync();

        // Calcular métricas y generar recomendaciones
        await _metricsService.CalculateMetricsAsync(request.VersionId);
        await _recommendationEngine.GenerateRecommendationsAsync(request.VersionId);

        await _auditService.LogActionAsync(null, "ResultadoPrueba", "CARGAR",
            $"VersionId: {request.VersionId}, Archivo: {request.NombreArchivo}");

        return CreatedAtAction(nameof(GetByVersion), new { versionId = request.VersionId }, new ResultadoPruebaDto
        {
            Id = resultado.Id,
            VersionId = resultado.VersionId,
            UsuarioCargaId = resultado.UsuarioCargaId,
            NombreArchivo = resultado.NombreArchivo,
            FormatoArchivo = resultado.FormatoArchivo,
            RutaArchivo = resultado.RutaArchivo,
            FechaCarga = resultado.FechaCarga,
            EstadoValidacion = resultado.EstadoValidacion,
            Observaciones = resultado.Observaciones
        });
    }

    [HttpPut("{id}/validar")]
    public async Task<IActionResult> Validar(int id, [FromBody] ValidarResultadoDto request)
    {
        var resultado = await _context.ResultadosPrueba.FirstOrDefaultAsync(r => r.Id == id);
        if (resultado == null)
            return NotFound();

        resultado.EstadoValidacion = request.EstadoValidacion;
        resultado.Observaciones = request.Observaciones ?? resultado.Observaciones;

        _context.ResultadosPrueba.Update(resultado);
        await _context.SaveChangesAsync();

        // Recalcular métricas y recomendaciones
        await _metricsService.CalculateMetricsAsync(resultado.VersionId);
        await _recommendationEngine.GenerateRecommendationsAsync(resultado.VersionId);

        await _auditService.LogActionAsync(null, "ResultadoPrueba", "VALIDAR",
            $"ID: {id}, Estado: {request.EstadoValidacion}");

        return NoContent();
    }
}

public class ValidarResultadoDto
{
    public required string EstadoValidacion { get; set; }
    public string? Observaciones { get; set; }
}
