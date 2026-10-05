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
public class ProyectosController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoriaService;
    private readonly ISuscripcionService _suscripcionService;
    private readonly IProyectoAccesoService _acceso;

    public ProyectosController(ApplicationDbContext context, IAuditoriaService auditoriaService, ISuscripcionService suscripcionService, IProyectoAccesoService acceso)
    {
        _context = context;
        _auditoriaService = auditoriaService;
        _suscripcionService = suscripcionService;
        _acceso = acceso;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProyectoDto>>> GetAll()
    {
        var proyectos = await _acceso.FiltrarProyectos(_context.Proyectos, User).ToListAsync();
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
        if (!_acceso.TieneAccesoAProyecto(User, id)) return Forbid();

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
        if (!_acceso.TieneAccesoAProyecto(User, id)) return Forbid();

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
    [Authorize(Policy = "gestionar_proyectos")]
    public async Task<ActionResult<ProyectoDto>> Create([FromBody] CreateProyectoDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // ── Verificar límite de plan ──────────────────────────────────────
        var limite = await _suscripcionService.VerificarLimiteProyectosAsync();
        if (!limite.Permitido)
            return StatusCode(402, new { message = limite.Mensaje, codigo = "LIMITE_PROYECTOS" });

        var nombreTrim = request.Nombre.Trim();
        if (await _context.Proyectos.AnyAsync(p => p.Nombre.ToLower() == nombreTrim.ToLower()))
            return BadRequest(new { message = "Ya existe un proyecto con ese nombre." });

        var proyecto = new Proyecto
        {
            Nombre = request.Nombre.Trim(),
            Descripcion = request.Descripcion,
            TipoSolucion = request.TipoSolucion,
            Estado = "activo"
        };

        // Proyecto y versión inicial se insertan en un solo SaveChangesAsync (una
        // sola transacción): antes eran dos operaciones separadas y un fallo entre
        // ambas dejaba un proyecto sin ninguna versión. Al asignar la navegación
        // (en vez de ProyectoId) EF Core resuelve el FK automáticamente sin
        // necesitar guardar el proyecto primero para conocer su Id.
        var versionInicial = new Models.Version
        {
            Proyecto = proyecto,
            NumeroVersion = request.VersionInicial ?? "1.0.0",
            Descripcion = $"Versión inicial de {proyecto.Nombre}",
            Estado = "pendiente"
        };
        _context.Proyectos.Add(proyecto);
        _context.Versiones.Add(versionInicial);
        await _context.SaveChangesAsync();

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

    private static readonly HashSet<string> EstadosProyectoValidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "activo", "inactivo", "archivado"
    };

    [HttpPut("{id}")]
    [Authorize(Policy = "gestionar_proyectos")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProyectoDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.Estado != null && !EstadosProyectoValidos.Contains(request.Estado))
            return BadRequest(new { message = "estado debe ser 'activo', 'inactivo' o 'archivado'." });

        if (request.Nombre != null && string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre no puede quedar vacío." });

        if (!_acceso.TieneAccesoAProyecto(User, id)) return Forbid();

        var proyecto = await _context.Proyectos.FirstOrDefaultAsync(p => p.Id == id);
        if (proyecto == null)
            return NotFound();

        if (request.Nombre != null)
        {
            var nombreTrim = request.Nombre.Trim();
            if (await _context.Proyectos.AnyAsync(p => p.Id != id && p.Nombre.ToLower() == nombreTrim.ToLower()))
                return BadRequest(new { message = "Ya existe un proyecto con ese nombre." });
        }

        var anterior = $"Nombre: {proyecto.Nombre}, Estado: {proyecto.Estado}";

        if (request.Nombre != null)       proyecto.Nombre       = request.Nombre.Trim();
        if (request.Descripcion != null)  proyecto.Descripcion  = request.Descripcion;
        if (request.TipoSolucion != null) proyecto.TipoSolucion = request.TipoSolucion;
        if (request.Estado != null)       proyecto.Estado       = request.Estado;

        _context.Proyectos.Update(proyecto);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Update", "Proyecto", id,
            $"Antes: [{anterior}] → Después: Nombre: {proyecto.Nombre}, Estado: {proyecto.Estado}");

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "gestionar_proyectos")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!_acceso.TieneAccesoAProyecto(User, id)) return Forbid();

        var proyecto = await _context.Proyectos.FirstOrDefaultAsync(p => p.Id == id);
        if (proyecto == null)
            return NotFound();

        _context.Proyectos.Remove(proyecto);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Delete", "Proyecto", id, $"Proyecto eliminado: {proyecto.Nombre}");

        return NoContent();
    }
}
