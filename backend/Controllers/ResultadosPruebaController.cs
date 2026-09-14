using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;
using System.Security.Claims;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ver_resultados")]
public class ResultadosPruebaController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoriaService;
    private readonly IIngestaResultadosService _ingesta;
    private readonly IProyectoAccesoService _acceso;

    public ResultadosPruebaController(
        ApplicationDbContext context,
        IAuditoriaService auditoriaService,
        IIngestaResultadosService ingesta,
        IProyectoAccesoService acceso)
    {
        _context = context;
        _auditoriaService = auditoriaService;
        _ingesta = ingesta;
        _acceso = acceso;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ResultadoPruebaDto>> GetById(int id)
    {
        if (!await _acceso.TieneAccesoAResultadoAsync(User, id)) return Forbid();

        var r = await _context.ResultadosPrueba.FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();

        return Ok(new ResultadoPruebaDto
        {
            Id = r.Id,
            VersionId = r.VersionId,
            UsuarioCargaId = r.UsuarioCargaId,
            NombreArchivo = r.NombreArchivo,
            FormatoArchivo = r.FormatoArchivo,
            RutaArchivo = r.RutaArchivo,
            FechaCarga = r.FechaCarga,
            EstadoValidacion = r.EstadoValidacion,
            Observaciones = r.Observaciones
        });
    }

    [HttpGet("version/{versionId}")]
    public async Task<ActionResult<List<ResultadoPruebaDto>>> GetByVersion(int versionId)
    {
        if (!await _acceso.TieneAccesoAVersionAsync(User, versionId)) return Forbid();

        var resultados = await _context.ResultadosPrueba
            .Where(r => r.VersionId == versionId)
            .ToListAsync();

        return Ok(resultados.Select(r => new ResultadoPruebaDto
        {
            Id = r.Id,
            VersionId = r.VersionId,
            UsuarioCargaId = r.UsuarioCargaId,
            NombreArchivo = r.NombreArchivo,
            FormatoArchivo = r.FormatoArchivo,
            RutaArchivo = r.RutaArchivo,
            FechaCarga = r.FechaCarga,
            EstadoValidacion = r.EstadoValidacion,
            Observaciones = r.Observaciones
        }).ToList());
    }

    [HttpPost]
    [Authorize(Policy = "cargar_resultados")]
    public async Task<ActionResult<ResultadoPruebaDto>> Create([FromBody] CreateResultadoPruebaDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!await _acceso.TieneAccesoAVersionAsync(User, request.VersionId)) return Forbid();

        var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        int? usuarioCargaId = usuarioIdClaim != null && int.TryParse(usuarioIdClaim.Value, out var uid) ? uid : null;

        var resultado = await _ingesta.IngestarAsync(request, usuarioCargaId);

        if (resultado.StatusCode == 201 && resultado.Body is ResultadoPruebaDto dto)
            return CreatedAtAction(nameof(GetByVersion), new { versionId = dto.VersionId }, dto);

        return StatusCode(resultado.StatusCode, resultado.Body);
    }

    // El endpoint /validar queda disponible para correcciones manuales de estado,
    // pero ya no recalcula métricas (los valores provienen del POST original).
    private static readonly HashSet<string> EstadosValidacionValidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "pendiente", "valido", "invalido"
    };

    [HttpPut("{id}/validar")]
    [Authorize(Policy = "cargar_resultados")]
    public async Task<IActionResult> Validar(int id, [FromBody] ValidarResultadoDto request)
    {
        if (!EstadosValidacionValidos.Contains(request.EstadoValidacion))
            return BadRequest(new { message = "estadoValidacion debe ser 'pendiente', 'valido' o 'invalido'." });

        if (!await _acceso.TieneAccesoAResultadoAsync(User, id)) return Forbid();

        var resultado = await _context.ResultadosPrueba.FirstOrDefaultAsync(r => r.Id == id);
        if (resultado == null)
            return NotFound();

        resultado.EstadoValidacion = request.EstadoValidacion;
        resultado.Observaciones    = request.Observaciones ?? resultado.Observaciones;

        _context.ResultadosPrueba.Update(resultado);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Update", "ResultadoPrueba", id,
            $"Validación manual, Estado: {request.EstadoValidacion}");

        return NoContent();
    }
}

public class ValidarResultadoDto
{
    public required string EstadoValidacion { get; set; }
    public string? Observaciones { get; set; }
}
