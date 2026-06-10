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
            Id = d.Id,
            RecomendacionId = d.RecomendacionId,
            UsuarioDecisorId = d.UsuarioDecisorId,
            DecisionFinal = d.DecisionFinal,
            Comentario = d.Comentario,
            FechaDecision = d.FechaDecision
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
