using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

/// <summary>
/// Gestión de reglas de evaluación (umbrales de calidad).
/// RF07 de la tesis: el Administrador puede configurar los umbrales
/// de calidad por proyecto (cobertura mínima, tasa de éxito mínima,
/// tiempo máximo de ejecución).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ver_evaluacion")]
public class ReglaEvaluacionController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public ReglaEvaluacionController(ApplicationDbContext context, IAuditoriaService auditoriaService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
    }

    // GET /api/reglaEvaluacion  — devuelve las reglas GLOBALES activas (id_proyecto = NULL)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var reglas = await _context.ReglasEvaluacion
            .Where(r => r.Estado == "activo" && r.ProyectoId == null)
            .OrderBy(r => r.Nombre)
            .Select(r => new
            {
                r.Id,
                r.Nombre,
                r.Descripcion,
                r.Criterio,
                r.Umbral,
                r.Estado,
                r.FechaCreacion,
            })
            .ToListAsync();

        return Ok(reglas);
    }

    /// <summary>
    /// GET /api/reglaEvaluacion/proyecto/{proyectoId}
    /// RF07/RF08/CU-03: vista efectiva de umbrales para un proyecto — para cada
    /// criterio activo, devuelve el override del proyecto si existe, o si no el
    /// valor global. `esPersonalizado` indica cuál de los dos es.
    /// </summary>
    [HttpGet("proyecto/{proyectoId}")]
    public async Task<IActionResult> GetByProyecto(int proyectoId)
    {
        var candidatas = await _context.ReglasEvaluacion
            .Where(r => r.Estado == "activo" && (r.ProyectoId == null || r.ProyectoId == proyectoId))
            .ToListAsync();

        var efectivas = candidatas
            .GroupBy(r => r.Criterio ?? "")
            .Select(g =>
            {
                var overrideProyecto = g.FirstOrDefault(r => r.ProyectoId == proyectoId);
                var global = g.FirstOrDefault(r => r.ProyectoId == null);
                var vigente = overrideProyecto ?? global;
                return new
                {
                    Criterio        = g.Key,
                    Nombre          = vigente?.Nombre,
                    Descripcion     = vigente?.Descripcion,
                    Umbral          = vigente?.Umbral,
                    UmbralGlobal    = global?.Umbral,
                    EsPersonalizado = overrideProyecto != null,
                    IdOverride      = overrideProyecto?.Id,
                };
            })
            .OrderBy(r => r.Criterio)
            .ToList();

        return Ok(efectivas);
    }

    /// <summary>
    /// PUT /api/reglaEvaluacion/proyecto/{proyectoId}
    /// Crea o actualiza el umbral específico de un proyecto para un criterio
    /// (RF07/CU-03). Solo Administrador.
    /// </summary>
    [HttpPut("proyecto/{proyectoId}")]
    [Authorize(Policy = "gestionar_reglas")]
    public async Task<IActionResult> UpsertProyecto(int proyectoId, [FromBody] UpsertReglaProyectoRequest request)
    {
        if (request.Umbral < 0)
            return BadRequest(new { message = "El umbral no puede ser negativo." });

        var proyecto = await _context.Proyectos.FirstOrDefaultAsync(p => p.Id == proyectoId);
        if (proyecto == null) return NotFound(new { message = "Proyecto no encontrado." });

        var reglaGlobal = await _context.ReglasEvaluacion
            .FirstOrDefaultAsync(r => r.ProyectoId == null && r.Criterio == request.Criterio);
        if (reglaGlobal == null)
            return BadRequest(new { message = $"No existe una regla global para el criterio '{request.Criterio}'." });

        var overrideExistente = await _context.ReglasEvaluacion
            .FirstOrDefaultAsync(r => r.ProyectoId == proyectoId && r.Criterio == request.Criterio);

        ReglaEvaluacion regla;
        bool esNuevo = overrideExistente == null;
        if (overrideExistente != null)
        {
            overrideExistente.Umbral = request.Umbral;
            overrideExistente.Estado = "activo";
            _context.ReglasEvaluacion.Update(overrideExistente);
            regla = overrideExistente;
        }
        else
        {
            regla = new ReglaEvaluacion
            {
                Nombre      = $"{reglaGlobal.Nombre} ({proyecto.Nombre})",
                Descripcion = reglaGlobal.Descripcion,
                Criterio    = request.Criterio,
                Umbral      = request.Umbral,
                Estado      = "activo",
                ProyectoId  = proyectoId,
            };
            _context.ReglasEvaluacion.Add(regla);
        }

        await _context.SaveChangesAsync();

        var accion = esNuevo ? "creado" : "actualizado";
        await _auditoriaService.RegistrarAsync("Update", "ReglaEvaluacion", regla.Id,
            $"Umbral personalizado {accion} — Proyecto: {proyecto.Nombre}, Criterio: {request.Criterio}, Umbral: {request.Umbral}");

        return Ok(new { message = "Umbral del proyecto actualizado correctamente.", umbral = request.Umbral });
    }

    /// <summary>
    /// DELETE /api/reglaEvaluacion/proyecto/{proyectoId}/{criterio}
    /// Elimina el override del proyecto para ese criterio; vuelve a regir el
    /// umbral global. Solo Administrador.
    /// </summary>
    [HttpDelete("proyecto/{proyectoId}/{criterio}")]
    [Authorize(Policy = "gestionar_reglas")]
    public async Task<IActionResult> DeleteOverrideProyecto(int proyectoId, string criterio)
    {
        var overrideExistente = await _context.ReglasEvaluacion
            .FirstOrDefaultAsync(r => r.ProyectoId == proyectoId && r.Criterio == criterio);

        if (overrideExistente == null)
            return NotFound(new { message = "Este proyecto no tiene un umbral personalizado para ese criterio." });

        var reglaId = overrideExistente.Id;
        var proyecto = await _context.Proyectos.FirstOrDefaultAsync(p => p.Id == proyectoId);

        _context.ReglasEvaluacion.Remove(overrideExistente);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Delete", "ReglaEvaluacion", reglaId,
            $"Umbral personalizado eliminado — Proyecto: {proyecto?.Nombre ?? proyectoId.ToString()}, Criterio: {criterio} (vuelve a regir el valor global)");

        return Ok(new { message = "Umbral personalizado eliminado; vuelve a regir el valor global." });
    }

    // GET /api/reglaEvaluacion/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var regla = await _context.ReglasEvaluacion.FirstOrDefaultAsync(r => r.Id == id);
        if (regla == null) return NotFound(new { message = "Regla no encontrada." });

        return Ok(new
        {
            regla.Id,
            regla.Nombre,
            regla.Descripcion,
            regla.Criterio,
            regla.Umbral,
            regla.Estado,
            regla.FechaCreacion,
        });
    }

    // PUT /api/reglaEvaluacion/{id}  — actualiza el umbral (solo Administrador)
    [HttpPut("{id}")]
    [Authorize(Policy = "gestionar_reglas")]
    public async Task<IActionResult> UpdateUmbral(int id, [FromBody] UpdateUmbralRequest request)
    {
        if (request.Umbral < 0)
            return BadRequest(new { message = "El umbral no puede ser negativo." });

        var regla = await _context.ReglasEvaluacion.FirstOrDefaultAsync(r => r.Id == id);
        if (regla == null) return NotFound(new { message = "Regla no encontrada." });

        var umbralAnterior = regla.Umbral;
        regla.Umbral      = request.Umbral;
        regla.Descripcion = request.Descripcion ?? regla.Descripcion;

        _context.ReglasEvaluacion.Update(regla);
        await _context.SaveChangesAsync();

        var alcance = regla.ProyectoId == null ? "global" : $"proyecto {regla.ProyectoId}";
        await _auditoriaService.RegistrarAsync("Update", "ReglaEvaluacion", regla.Id,
            $"Regla '{regla.Nombre}' ({alcance}) — Umbral: {umbralAnterior} → {request.Umbral}");

        return Ok(new { message = "Umbral actualizado correctamente.", umbral = regla.Umbral });
    }
}

public record UpdateUmbralRequest(decimal Umbral, string? Descripcion);
public record UpsertReglaProyectoRequest(string Criterio, decimal Umbral);
