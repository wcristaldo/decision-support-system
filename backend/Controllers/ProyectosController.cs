using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProyectosController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;

    public ProyectosController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProyectoDto>>> GetAll()
    {
        var proyectos = await _context.Proyectos.ToListAsync();
        return Ok(proyectos.Select(p => new ProyectoDto
        {
            Id = p.Id,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            TipoSolucion = p.TipoSolucion,
            Estado = p.Estado,
            FechaCreacion = p.FechaCreacion
        }).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProyectoDto>> GetById(int id)
    {
        var proyecto = await _context.Proyectos.FirstOrDefaultAsync(p => p.Id == id);
        if (proyecto == null)
            return NotFound();

        return Ok(new ProyectoDto
        {
            Id = proyecto.Id,
            Nombre = proyecto.Nombre,
            Descripcion = proyecto.Descripcion,
            TipoSolucion = proyecto.TipoSolucion,
            Estado = proyecto.Estado,
            FechaCreacion = proyecto.FechaCreacion
        });
    }

    [HttpPost]
    public async Task<ActionResult<ProyectoDto>> Create([FromBody] CreateProyectoDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var proyecto = new Proyecto
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            TipoSolucion = request.TipoSolucion,
            Estado = "activo"
        };

        _context.Proyectos.Add(proyecto);
        await _context.SaveChangesAsync();

        await _auditService.LogActionAsync(null, "Proyecto", "CREAR", $"ID: {proyecto.Id}, Nombre: {proyecto.Nombre}");

        return CreatedAtAction(nameof(GetById), new { id = proyecto.Id }, new ProyectoDto
        {
            Id = proyecto.Id,
            Nombre = proyecto.Nombre,
            Descripcion = proyecto.Descripcion,
            TipoSolucion = proyecto.TipoSolucion,
            Estado = proyecto.Estado,
            FechaCreacion = proyecto.FechaCreacion
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProyectoDto request)
    {
        var proyecto = await _context.Proyectos.FirstOrDefaultAsync(p => p.Id == id);
        if (proyecto == null)
            return NotFound();

        var anterior = $"Nombre: {proyecto.Nombre}, Estado: {proyecto.Estado}";

        if (!string.IsNullOrEmpty(request.Nombre))       proyecto.Nombre       = request.Nombre;
        if (!string.IsNullOrEmpty(request.Descripcion))  proyecto.Descripcion  = request.Descripcion;
        if (!string.IsNullOrEmpty(request.TipoSolucion)) proyecto.TipoSolucion = request.TipoSolucion;
        if (!string.IsNullOrEmpty(request.Estado))       proyecto.Estado       = request.Estado;

        _context.Proyectos.Update(proyecto);
        await _context.SaveChangesAsync();

        await _auditService.LogActionAsync(null, "Proyecto", "ACTUALIZAR",
            $"Antes: [{anterior}] → Después: Nombre: {proyecto.Nombre}, Estado: {proyecto.Estado}");

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var proyecto = await _context.Proyectos.FirstOrDefaultAsync(p => p.Id == id);
        if (proyecto == null)
            return NotFound();

        _context.Proyectos.Remove(proyecto);
        await _context.SaveChangesAsync();

        await _auditService.LogActionAsync(null, "Proyecto", "ELIMINAR", $"ID: {proyecto.Id}, Nombre: {proyecto.Nombre}");

        return NoContent();
    }
}
