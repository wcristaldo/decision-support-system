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

    [HttpGet("version/{versionId}")]
    public async Task<ActionResult<List<DecisionDespliegueDto>>> GetByVersion(int versionId)
    {
        var decisiones = await _context.DecisionesDespliegue
            .Where(d => d.VersionId == versionId)
            .ToListAsync();

        return Ok(decisiones.Select(d => new DecisionDespliegueDto
        {
            Id = d.Id,
            VersionId = d.VersionId,
            UsuarioId = d.UsuarioId,
            Decision = d.Decision,
            Justificacion = d.Justificacion,
            FechaDecision = d.FechaDecision
        }).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<DecisionDespliegueDto>> Create([FromBody] CreateDecisionDespliegueDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Obtener ID del usuario del contexto (en implementación real, del JWT)
        var usuarioId = 1; // Por ahora usar usuario por defecto

        var decision = new DecisionDespliegue
        {
            VersionId = request.VersionId,
            UsuarioId = usuarioId,
            Decision = request.Decision,
            Justificacion = request.Justificacion
        };

        _context.DecisionesDespliegue.Add(decision);
        await _context.SaveChangesAsync();

        await _auditService.LogActionAsync(usuarioId, "DecisionDespliegue", "TOMAR_DECISION", null, $"VersionId: {request.VersionId}, Decision: {request.Decision}");

        return CreatedAtAction(nameof(GetByVersion), new { versionId = request.VersionId }, new DecisionDespliegueDto
        {
            Id = decision.Id,
            VersionId = decision.VersionId,
            UsuarioId = decision.UsuarioId,
            Decision = decision.Decision,
            Justificacion = decision.Justificacion,
            FechaDecision = decision.FechaDecision
        });
    }
}
