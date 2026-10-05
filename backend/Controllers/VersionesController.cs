using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ver_proyectos")]
public class VersionesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoriaService;
    private readonly IProyectoAccesoService _acceso;

    public VersionesController(ApplicationDbContext context, IAuditoriaService auditoriaService, IProyectoAccesoService acceso)
    {
        _context = context;
        _auditoriaService = auditoriaService;
        _acceso = acceso;
    }

    [HttpGet("proyecto/{proyectoId}")]
    public async Task<ActionResult<List<VersionDto>>> GetByProyecto(int proyectoId)
    {
        if (!_acceso.TieneAccesoAProyecto(User, proyectoId)) return Forbid();

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
        if (!await _acceso.TieneAccesoAVersionAsync(User, id)) return Forbid();

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
    [Authorize(Policy = "gestionar_proyectos")]
    public async Task<ActionResult<VersionDto>> Create([FromBody] CreateVersionDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var proyectoExiste = await _context.Proyectos.AnyAsync(p => p.Id == request.ProyectoId);
        if (!proyectoExiste)
            return NotFound(new { message = "El proyecto indicado no existe." });

        if (!_acceso.TieneAccesoAProyecto(User, request.ProyectoId)) return Forbid();

        var numeroTrim = request.NumeroVersion.Trim();
        var yaExiste = await _context.Versiones.AnyAsync(v =>
            v.ProyectoId == request.ProyectoId && v.NumeroVersion.ToLower() == numeroTrim.ToLower());
        if (yaExiste)
            return BadRequest(new { message = $"Ya existe la versión {numeroTrim} para este proyecto." });

        var version = new Models.Version
        {
            ProyectoId = request.ProyectoId,
            NumeroVersion = numeroTrim,
            Descripcion = request.Descripcion
        };

        _context.Versiones.Add(version);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Create", "Version", version.Id, $"Numero: {version.NumeroVersion}");

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

    private static readonly HashSet<string> EstadosVersionValidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "pendiente", "en_evaluacion", "aprobada", "rechazada", "desplegada"
    };

    [HttpPut("{id}")]
    [Authorize(Policy = "gestionar_proyectos")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateVersionDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.Estado != null && !EstadosVersionValidos.Contains(request.Estado))
            return BadRequest(new { message = "estado debe ser 'pendiente', 'en_evaluacion', 'aprobada', 'rechazada' o 'desplegada'." });

        if (!await _acceso.TieneAccesoAVersionAsync(User, id)) return Forbid();

        var version = await _context.Versiones.FirstOrDefaultAsync(v => v.Id == id);
        if (version == null)
            return NotFound();

        if (request.NumeroVersion != null)
        {
            var numeroTrim = request.NumeroVersion.Trim();
            var yaExiste = await _context.Versiones.AnyAsync(v =>
                v.Id != id && v.ProyectoId == version.ProyectoId && v.NumeroVersion.ToLower() == numeroTrim.ToLower());
            if (yaExiste)
                return BadRequest(new { message = $"Ya existe la versión {numeroTrim} para este proyecto." });
        }

        var anterior = $"Numero: {version.NumeroVersion}";

        if (request.NumeroVersion != null) version.NumeroVersion = request.NumeroVersion.Trim();
        if (request.Descripcion != null)   version.Descripcion   = request.Descripcion;
        if (request.Estado != null)        version.Estado        = request.Estado;

        _context.Versiones.Update(version);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Update", "Version", id,
            $"Antes: [{anterior}] → Después: Numero: {version.NumeroVersion}");

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "gestionar_proyectos")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _acceso.TieneAccesoAVersionAsync(User, id)) return Forbid();

        var version = await _context.Versiones.FirstOrDefaultAsync(v => v.Id == id);
        if (version == null)
            return NotFound();

        _context.Versiones.Remove(version);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Delete", "Version", id, $"Numero: {version.NumeroVersion}");

        return NoContent();
    }
}
