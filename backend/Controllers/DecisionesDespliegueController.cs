using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DecisionesDespliegueController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public DecisionesDespliegueController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet("recomendacion/{recomendacionId}")]
    public async Task<ActionResult<List<DecisionDespliegueDto>>> GetByRecomendacion(int recomendacionId)
    {
        var decisiones = await _context.DecisionesDespliegue
            .Where(d => d.RecomendacionId == recomendacionId)
            .ToListAsync();

        return Ok(decisiones.Select(d => new DecisionDespliegueDto
        {
            Id               = d.Id,
            RecomendacionId  = d.RecomendacionId,
            UsuarioDecisorId = d.UsuarioDecisorId,
            DecisionFinal    = d.DecisionFinal,
            Comentario       = d.Comentario,
            FechaDecision    = d.FechaDecision
        }).ToList());
    }

    /// <summary>
    /// GET /api/decisionesDespliegue/version/{versionId}
    /// Devuelve todas las decisiones de despliegue registradas para una versión,
    /// recorriendo la cadena: version → resultados_prueba → evaluaciones
    /// → recomendaciones → decisiones_despliegue.
    /// Usado por la página AnalisisVersion.jsx.
    /// </summary>
    [HttpGet("version/{versionId}")]
    public async Task<ActionResult<List<DecisionDespliegueDto>>> GetByVersion(int versionId)
    {
        var resultadoIds = await _context.ResultadosPrueba
            .Where(r => r.VersionId == versionId)
            .Select(r => r.Id)
            .ToListAsync();

        var evaluacionIds = await _context.Evaluaciones
            .Where(e => resultadoIds.Contains(e.ResultadoId))
            .Select(e => e.Id)
            .ToListAsync();

        var recomendacionIds = await _context.Recomendaciones
            .Where(r => evaluacionIds.Contains(r.EvaluacionId))
            .Select(r => r.Id)
            .ToListAsync();

        var decisiones = await _context.DecisionesDespliegue
            .Where(d => recomendacionIds.Contains(d.RecomendacionId))
            .OrderByDescending(d => d.FechaDecision)
            .ToListAsync();

        return Ok(decisiones.Select(d => new DecisionDespliegueDto
        {
            Id               = d.Id,
            RecomendacionId  = d.RecomendacionId,
            UsuarioDecisorId = d.UsuarioDecisorId,
            DecisionFinal    = d.DecisionFinal,
            Comentario       = d.Comentario,
            FechaDecision    = d.FechaDecision
        }).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<DecisionDespliegueDto>> Create([FromBody] CreateDecisionDespliegueDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var decision = new DecisionDespliegue
        {
            RecomendacionId = request.RecomendacionId,
            DecisionFinal = request.DecisionFinal,
            Comentario = request.Comentario
        };

        _context.DecisionesDespliegue.Add(decision);
        await _context.SaveChangesAsync();

        await _auditService.LogActionAsync(null, "DecisionDespliegue", "TOMAR_DECISION",
            $"RecomendacionId: {request.RecomendacionId}, Decision: {request.DecisionFinal}");

        return CreatedAtAction(nameof(GetByRecomendacion), new { recomendacionId = request.RecomendacionId }, new DecisionDespliegueDto
        {
            Id = decision.Id,
            RecomendacionId = decision.RecomendacionId,
            UsuarioDecisorId = decision.UsuarioDecisorId,
            DecisionFinal = decision.DecisionFinal,
            Comentario = decision.Comentario,
            FechaDecision = decision.FechaDecision
        });
    }
}
