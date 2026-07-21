using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using System.Security.Claims;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditoriaController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AuditoriaController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<dynamic>> GetAuditoria(
        [FromQuery] string? usuario,
        [FromQuery] string? accion,
        [FromQuery] string? entidad,
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] int pagina = 1,
        [FromQuery] int limite = 50)
    {
        var isAdmin = User.IsInRole("ADMIN") || User.IsInRole("Administrador");

        if (!isAdmin)
            return Forbid("Solo administradores pueden acceder a la auditoría");

        var query = _context.Auditoria.AsQueryable();

        if (!string.IsNullOrWhiteSpace(usuario))
            query = query.Where(a => a.Usuario != null && a.Usuario.Nombre.ToLower().Contains(usuario.ToLower()));

        if (!string.IsNullOrWhiteSpace(accion))
            query = query.Where(a => a.Accion != null && a.Accion.ToLower().Contains(accion.ToLower()));

        if (!string.IsNullOrWhiteSpace(entidad))
            query = query.Where(a => a.EntidadAfectada != null && a.EntidadAfectada.ToLower().Contains(entidad.ToLower()));

        if (fechaDesde.HasValue)
            query = query.Where(a => a.FechaEvento >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(a => a.FechaEvento <= fechaHasta.Value);

        var total = await query.CountAsync();
        var registros = await query
            .Include(a => a.Usuario)
            .OrderByDescending(a => a.FechaEvento)
            .Skip((pagina - 1) * limite)
            .Take(limite)
            .Select(a => new AuditoriaDto
            {
                Id = a.Id,
                UsuarioId = a.UsuarioId,
                Usuario = a.Usuario != null ? a.Usuario.Nombre : null,
                EntidadAfectada = a.EntidadAfectada,
                IdRegistroAfectado = a.IdRegistroAfectado,
                Accion = a.Accion,
                Detalle = a.Detalle,
                FechaEvento = a.FechaEvento,
                IpOrigen = a.IpOrigen
            })
            .ToListAsync();

        return Ok(new
        {
            registros,
            total,
            pagina,
            limite,
            totalPaginas = Math.Ceiling((double)total / limite)
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AuditoriaDto>> GetById(int id)
    {
        var isAdmin = User.IsInRole("ADMIN") || User.IsInRole("Administrador");

        if (!isAdmin)
            return Forbid("Solo administradores pueden acceder a la auditoría");

        var auditoria = await _context.Auditoria
            .Include(a => a.Usuario)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auditoria == null)
            return NotFound();

        return Ok(new AuditoriaDto
        {
            Id = auditoria.Id,
            UsuarioId = auditoria.UsuarioId,
            Usuario = auditoria.Usuario != null ? auditoria.Usuario.Nombre : null,
            EntidadAfectada = auditoria.EntidadAfectada,
            IdRegistroAfectado = auditoria.IdRegistroAfectado,
            Accion = auditoria.Accion,
            Detalle = auditoria.Detalle,
            FechaEvento = auditoria.FechaEvento,
            IpOrigen = auditoria.IpOrigen
        });
    }

    [HttpGet("estadisticas")]
    public async Task<ActionResult<dynamic>> GetEstadisticas([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var isAdmin = User.IsInRole("ADMIN") || User.IsInRole("Administrador");

        if (!isAdmin)
            return Forbid("Solo administradores pueden acceder a la auditoría");

        var query = _context.Auditoria.AsQueryable();

        if (desde.HasValue)
            query = query.Where(a => a.FechaEvento >= desde.Value);

        if (hasta.HasValue)
            query = query.Where(a => a.FechaEvento <= hasta.Value);

        var acciones = await query
            .GroupBy(a => a.Accion)
            .Select(g => new { accion = g.Key, cantidad = g.Count() })
            .ToListAsync();

        var entidades = await query
            .GroupBy(a => a.EntidadAfectada)
            .Select(g => new { entidad = g.Key, cantidad = g.Count() })
            .ToListAsync();

        var usuarios = await query
            .GroupBy(a => a.UsuarioId)
            .Select(g => new { usuarioId = g.Key, cantidad = g.Count() })
            .ToListAsync();

        var totalEventos = await query.CountAsync();

        return Ok(new
        {
            totalEventos,
            porAccion = acciones,
            porEntidad = entidades,
            porUsuario = usuarios
        });
    }
}
