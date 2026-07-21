using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;

namespace DecisionSupportAPI.Controllers;

/// <summary>
/// Gestión de reglas de evaluación (umbrales de calidad).
/// RF07 de la tesis: el Administrador puede configurar los umbrales
/// de calidad por proyecto (cobertura mínima, tasa de éxito mínima,
/// tiempo máximo de ejecución).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReglaEvaluacionController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ReglaEvaluacionController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET /api/reglaEvaluacion  — devuelve todas las reglas activas
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var reglas = await _context.ReglasEvaluacion
            .Where(r => r.Estado == "activo")
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
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> UpdateUmbral(int id, [FromBody] UpdateUmbralRequest request)
    {
        if (request.Umbral < 0)
            return BadRequest(new { message = "El umbral no puede ser negativo." });

        var regla = await _context.ReglasEvaluacion.FirstOrDefaultAsync(r => r.Id == id);
        if (regla == null) return NotFound(new { message = "Regla no encontrada." });

        regla.Umbral      = request.Umbral;
        regla.Descripcion = request.Descripcion ?? regla.Descripcion;

        _context.ReglasEvaluacion.Update(regla);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Umbral actualizado correctamente.", umbral = regla.Umbral });
    }
}

public record UpdateUmbralRequest(decimal Umbral, string? Descripcion);
