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
    private readonly IAuditoriaService _auditoriaService;
    private readonly ISuscripcionService _suscripcionService;

    public ProyectosController(ApplicationDbContext context, IAuditService auditService, IAuditoriaService auditoriaService, ISuscripcionService suscripcionService)
    {
        _context = context;
        _auditService = auditService;
        _auditoriaService = auditoriaService;
        _suscripcionService = suscripcionService;
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

    [HttpGet("{id}/versiones")]
    public async Task<ActionResult<List<VersionDto>>> GetVersiones(int id)
    {
        var proyecto = await _context.Proyectos.FirstOrDefaultAsync(p => p.Id == id);
        if (proyecto == null)
            return NotFound(new { message = "Proyecto no encontrado" });

        var versiones = await _context.Versiones
            .Where(v => v.ProyectoId == id)
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

    [HttpPost]
    public async Task<ActionResult<ProyectoDto>> Create([FromBody] CreateProyectoDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // ── Verificar límite de plan ──────────────────────────────────────
        var limite = await _suscripcionService.VerificarLimiteProyectosAsync();
        if (!limite.Permitido)
            return StatusCode(402, new { message = limite.Mensaje, codigo = "LIMITE_PROYECTOS" });

        var proyecto = new Proyecto
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            TipoSolucion = request.TipoSolucion,
            Estado = "activo"
        };

        _context.Proyectos.Add(proyecto);
        await _context.SaveChangesAsync();

        var versionInicial = new Models.Version
        {
            ProyectoId = proyecto.Id,
            NumeroVersion = request.VersionInicial ?? "1.0.0",
            Descripcion = $"Versión inicial de {proyecto.Nombre}",
            Estado = "pendiente"
        };
        _context.Versiones.Add(versionInicial);
        await _context.SaveChangesAsync();

        await _auditService.LogActionAsync(null, "Proyecto", "CREAR", $"ID: {proyecto.Id}, Nombre: {proyecto.Nombre}");
        await _auditoriaService.RegistrarAsync("Create", "Proyecto", proyecto.Id, $"Proyecto creado: {proyecto.Nombre}");

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

        if (request.Nombre != null)       proyecto.Nombre       = request.Nombre;
        if (request.Descripcion != null)  proyecto.Descripcion  = request.Descripcion;
        if (request.TipoSolucion != null) proyecto.TipoSolucion = request.TipoSolucion;
        if (request.Estado != null)       proyecto.Estado       = request.Estado;

        _context.Proyectos.Update(proyecto);
        await _context.SaveChangesAsync();

        await _auditService.LogActionAsync(null, "Proyecto", "ACTUALIZAR",
            $"Antes: [{anterior}] → Después: Nombre: {proyecto.Nombre}, Estado: {proyecto.Estado}");
        await _auditoriaService.RegistrarAsync("Update", "Proyecto", id, $"Proyecto actualizado: {proyecto.Nombre}");

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
        await _auditoriaService.RegistrarAsync("Delete", "Proyecto", id, $"Proyecto eliminado: {proyecto.Nombre}");

        return NoContent();
    }
}
