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
    private readonly ISuscripcionService _suscripcionService;

    public ResultadosPruebaController(
        ApplicationDbContext context,
        IMetricsCalculationService metricsService,
        IRecommendationEngine recommendationEngine,
        IAuditService auditService,
        ISuscripcionService suscripcionService)
    {
        _context = context;
        _metricsService = metricsService;
        _recommendationEngine = recommendationEngine;
        _auditService = auditService;
        _suscripcionService = suscripcionService;
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

        // ── Verificar límite de plan ──────────────────────────────────────
        var limite = await _suscripcionService.VerificarLimiteEvaluacionesMesAsync();
        if (!limite.Permitido)
            return StatusCode(402, new { message = limite.Mensaje, codigo = "LIMITE_EVALUACIONES" });

        // Validar coherencia de pruebas
        if (request.PruebasExitosas + request.PruebasFallidas > request.TotalPruebas)
            return BadRequest(new { message = "La suma de exitosas y fallidas no puede superar el total de pruebas." });

        var version = await _context.Versiones.FirstOrDefaultAsync(v => v.Id == request.VersionId);
        if (version == null)
            return NotFound(new { message = "Versión no encontrada." });

        var resultado = new ResultadoPrueba
        {
            VersionId        = request.VersionId,
            NombreArchivo    = request.NombreArchivo,
            FormatoArchivo   = request.FormatoArchivo ?? "JSON",
            RutaArchivo      = request.RutaArchivo,
            Observaciones    = request.Observaciones,
            EstadoValidacion = "valido"    // se marca válido al ingresar los datos
        };

        _context.ResultadosPrueba.Add(resultado);
        await _context.SaveChangesAsync();

        // Calcular métricas reales y generar recomendación automática
        await _metricsService.CalculateMetricsAsync(
            resultado.Id,
            request.TotalPruebas,
            request.PruebasExitosas,
            request.PruebasFallidas,
            request.Cobertura,
            request.TiempoEjecucion);

        await _recommendationEngine.GenerateRecommendationsAsync(request.VersionId);

        await _auditService.LogActionAsync(null, "ResultadoPrueba", "CARGAR",
            $"VersionId: {request.VersionId}, Archivo: {request.NombreArchivo}, " +
            $"Total: {request.TotalPruebas}, Exitosas: {request.PruebasExitosas}, " +
            $"Cobertura: {request.Cobertura}%");

        return CreatedAtAction(nameof(GetByVersion), new { versionId = request.VersionId }, new ResultadoPruebaDto
        {
            Id               = resultado.Id,
            VersionId        = resultado.VersionId,
            UsuarioCargaId   = resultado.UsuarioCargaId,
            NombreArchivo    = resultado.NombreArchivo,
            FormatoArchivo   = resultado.FormatoArchivo,
            RutaArchivo      = resultado.RutaArchivo,
            FechaCarga       = resultado.FechaCarga,
            EstadoValidacion = resultado.EstadoValidacion,
            Observaciones    = resultado.Observaciones
        });
    }

    // El endpoint /validar queda disponible para correcciones manuales de estado,
    // pero ya no recalcula métricas (los valores provienen del POST original).
    [HttpPut("{id}/validar")]
    public async Task<IActionResult> Validar(int id, [FromBody] ValidarResultadoDto request)
    {
        var resultado = await _context.ResultadosPrueba.FirstOrDefaultAsync(r => r.Id == id);
        if (resultado == null)
            return NotFound();

        resultado.EstadoValidacion = request.EstadoValidacion;
        resultado.Observaciones    = request.Observaciones ?? resultado.Observaciones;

        _context.ResultadosPrueba.Update(resultado);
        await _context.SaveChangesAsync();

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
