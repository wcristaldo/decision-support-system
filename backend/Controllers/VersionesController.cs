using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VersionesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public VersionesController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet("proyecto/{proyectoId}")]
    public async Task<ActionResult<List<VersionDto>>> GetByProyecto(int proyectoId)
    {
        var versiones = await _context.Versiones
            .Where(v => v.ProyectoId == proyectoId)
            .ToListAsync();

        return Ok(versiones.Select(v => new VersionDto
        {
            Id = v.Id,
            ProyectoId = v.ProyectoId,
            NumeroVersion = v.NumeroVersion,
            Descripcion = v.Descripcion,
            FechaVersion = v.FechaVersion,
            Estado = v.Estado
        }).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VersionDto>> GetById(int id)
    {
        var version = await _context.Versiones.FirstOrDefaultAsync(v => v.Id == id);
        if (version == null)
            return NotFound();

        return Ok(new VersionDto
        {
            Id = version.Id,
            ProyectoId = version.ProyectoId,
            NumeroVersion = version.NumeroVersion,
            Descripcion = version.Descripcion,
            FechaVersion = version.FechaVersion,
            Estado = version.Estado
        });
    }

    [HttpPost]
    public async Task<ActionResult<VersionDto>> Create([FromBody] CreateVersionDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var version = new Models.Version
        {
            ProyectoId = request.ProyectoId,
            NumeroVersion = request.NumeroVersion,
            Descripcion = request.Descripcion
        };

        _context.Versiones.Add(version);
        await _context.SaveChangesAsync();

        await _auditService.LogActionAsync(null, "Version", "CREAR", $"ID: {version.Id}, Numero: {version.NumeroVersion}");

        return CreatedAtAction(nameof(GetById), new { id = version.Id }, new VersionDto
        {
            Id = version.Id,
            ProyectoId = version.ProyectoId,
            NumeroVersion = version.NumeroVersion,
            Descripcion = version.Descripcion,
            FechaVersion = version.FechaVersion,
            Estado = version.Estado
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateVersionDto request)
    {
        var version = await _context.Versiones.FirstOrDefaultAsync(v => v.Id == id);
        if (version == null)
            return NotFound();

        var anterior = $"Numero: {version.NumeroVersion}";

        if (!string.IsNullOrEmpty(request.NumeroVersion)) version.NumeroVersion = request.NumeroVersion;
        if (!string.IsNullOrEmpty(request.Descripcion))   version.Descripcion   = request.Descripcion;
        if (!string.IsNullOrEmpty(request.Estado))        version.Estado        = request.Estado;

        _context.Versiones.Update(version);
        await _context.SaveChangesAsync();

        await _auditService.LogActionAsync(null, "Version", "ACTUALIZAR",
            $"Antes: [{anterior}] → Después: Numero: {version.NumeroVersion}");

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var version = await _context.Versiones.FirstOrDefaultAsync(v => v.Id == id);
        if (version == null)
            return NotFound();

        _context.Versiones.Remove(version);
        await _context.SaveChangesAsync();

        await _auditService.LogActionAsync(null, "Version", "ELIMINAR", $"ID: {version.Id}, Numero: {version.NumeroVersion}");

        return NoContent();
    }
}
