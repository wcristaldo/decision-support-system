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
[Authorize(Policy = "ver_decisiones")]
public class DecisionesDespliegueController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoriaService;
    private readonly IActaPdfService _actaPdfService;

    // Longitud mínima exigida a la justificación cuando la decisión contradice
    // la recomendación del sistema (RF12/RF13: "el sistema asiste, no decide" —
    // apartarse de la recomendación requiere una justificación real, no un trámite).
    private const int MinLargoJustificacionOverride = 20;

    public DecisionesDespliegueController(ApplicationDbContext context, IAuditoriaService auditoriaService, IActaPdfService actaPdfService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
        _actaPdfService = actaPdfService;
    }

    private static DecisionDespliegueDto ToDto(DecisionDespliegue d) => new()
    {
        Id               = d.Id,
        RecomendacionId  = d.RecomendacionId,
        UsuarioDecisorId = d.UsuarioDecisorId,
        DecisionFinal    = d.DecisionFinal,
        Comentario       = d.Comentario,
        EsOverride       = d.EsOverride,
        FechaDecision    = d.FechaDecision
    };

    /// <summary>
    /// Determina si una decisión contradice la recomendación del sistema:
    /// aprobar algo que el motor marcó "no apto", o rechazar algo que marcó "apto".
    /// "desplegar_con_observaciones" (zona gris) y "postergado" nunca cuentan como
    /// override — el sistema ya no fue taxativo, o el humano tampoco se definió.
    /// </summary>
    private static bool CalcularEsOverride(string decisionFinal, string? tipoRecomendacion)
    {
        var d = decisionFinal.Trim().ToLowerInvariant();
        var r = (tipoRecomendacion ?? "").Trim().ToLowerInvariant();
        if (d == "aprobado" && r == "no_desplegar") return true;
        if (d == "rechazado" && r == "desplegar") return true;
        return false;
    }

    [HttpGet("recomendacion/{recomendacionId}")]
    public async Task<ActionResult<List<DecisionDespliegueDto>>> GetByRecomendacion(int recomendacionId)
    {
        var decisiones = await _context.DecisionesDespliegue
            .Where(d => d.RecomendacionId == recomendacionId)
            .ToListAsync();

        return Ok(decisiones.Select(ToDto).ToList());
    }

    /// <summary>
    /// GET /api/decisionesDespliegue/version/{versionId}
    /// Devuelve todas las decisiones de despliegue registradas para una versión,
    /// recorriendo la cadena: version → resultados_prueba → evaluaciones
    /// → recomendaciones → decisiones_despliegue.
    /// Usado por la página AnalisisVersion.jsx.
    /// </summary>
    [HttpGet("version/{versionId}")]
    public async Task<ActionResult<List<DecisionDespliegueDto>>> GetByVersion(int versionId)
    {
        var resultadoIds = await _context.ResultadosPrueba
            .Where(r => r.VersionId == versionId)
            .Select(r => r.Id)
            .ToListAsync();

        var evaluacionIds = await _context.Evaluaciones
            .Where(e => resultadoIds.Contains(e.ResultadoId))
            .Select(e => e.Id)
            .ToListAsync();

        var recomendacionIds = await _context.Recomendaciones
            .Where(r => evaluacionIds.Contains(r.EvaluacionId))
            .Select(r => r.Id)
            .ToListAsync();

        var decisiones = await _context.DecisionesDespliegue
            .Where(d => recomendacionIds.Contains(d.RecomendacionId))
            .OrderByDescending(d => d.FechaDecision)
            .ToListAsync();

        return Ok(decisiones.Select(ToDto).ToList());
    }

    /// <summary>
    /// GET /api/decisionesDespliegue/resultado/{resultadoId}
    /// Devuelve las decisiones de despliegue asociadas a un resultado específico.
    /// </summary>
    [HttpGet("resultado/{resultadoId}")]
    public async Task<ActionResult<List<DecisionDespliegueDto>>> GetByResultado(int resultadoId)
    {
        var evaluacionIds = await _context.Evaluaciones
            .Where(e => e.ResultadoId == resultadoId)
            .Select(e => e.Id)
            .ToListAsync();

        var recomendacionIds = await _context.Recomendaciones
            .Where(r => evaluacionIds.Contains(r.EvaluacionId))
            .Select(r => r.Id)
            .ToListAsync();

        var decisiones = await _context.DecisionesDespliegue
            .Where(d => recomendacionIds.Contains(d.RecomendacionId))
            .OrderByDescending(d => d.FechaDecision)
            .ToListAsync();

        return Ok(decisiones.Select(ToDto).ToList());
    }

    /// <summary>
    /// GET /api/decisionesDespliegue/adherencia
    /// Indicador histórico de cuánto sigue el equipo la recomendación del sistema:
    /// % de decisiones (aprobado/rechazado) que NO contradijeron la recomendación.
    /// Es el dato de investigación que mide si el DSS realmente influye en las
    /// decisiones (Capítulo de resultados de la tesis).
    /// </summary>
    [HttpGet("adherencia")]
    public async Task<IActionResult> GetAdherencia()
    {
        var decisiones = await _context.DecisionesDespliegue
            .Where(d => d.DecisionFinal == "aprobado" || d.DecisionFinal == "rechazado")
            .ToListAsync();

        var total = decisiones.Count;
        var overrides = decisiones.Count(d => d.EsOverride);
        var alineadas = total - overrides;
        decimal? porcentaje = total > 0 ? Math.Round((decimal)alineadas / total * 100, 1) : null;

        return Ok(new
        {
            total,
            alineadas,
            overrides,
            porcentajeAdherencia = porcentaje
        });
    }

    [HttpPost]
    [Authorize(Policy = "registrar_decision")]
    public async Task<ActionResult<DecisionDespliegueDto>> Create([FromBody] CreateDecisionDespliegueDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var decisionNormalizada = request.DecisionFinal.Trim().ToLowerInvariant();
        if (decisionNormalizada != "aprobado" && decisionNormalizada != "rechazado" && decisionNormalizada != "postergado")
            return BadRequest(new { message = "decisionFinal debe ser 'aprobado', 'rechazado' o 'postergado'." });

        if (string.IsNullOrWhiteSpace(request.Comentario))
            return BadRequest(new { message = "La justificación es obligatoria." });

        var recomendacion = await _context.Recomendaciones.FirstOrDefaultAsync(r => r.Id == request.RecomendacionId);
        if (recomendacion == null)
            return NotFound(new { message = "Recomendación no encontrada." });

        var esOverride = CalcularEsOverride(decisionNormalizada, recomendacion.TipoRecomendacion);
        if (esOverride && request.Comentario.Trim().Length < MinLargoJustificacionOverride)
        {
            return BadRequest(new
            {
                message = $"Esta decisión contradice la recomendación del sistema. " +
                          $"La justificación debe tener al menos {MinLargoJustificacionOverride} caracteres explicando el motivo.",
                codigo = "JUSTIFICACION_INSUFICIENTE_OVERRIDE"
            });
        }

        var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        int? usuarioDecisorId = usuarioIdClaim != null && int.TryParse(usuarioIdClaim.Value, out var uid) ? uid : null;

        var decision = new DecisionDespliegue
        {
            RecomendacionId = request.RecomendacionId,
            UsuarioDecisorId = usuarioDecisorId,
            DecisionFinal = decisionNormalizada,
            Comentario = request.Comentario.Trim(),
            EsOverride = esOverride
        };

        _context.DecisionesDespliegue.Add(decision);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Create", "DecisionDespliegue", decision.Id,
            $"RecomendacionId: {request.RecomendacionId}, Decision: {decisionNormalizada}" +
            (esOverride ? " (OVERRIDE: contradice la recomendación del sistema)" : ""));

        return CreatedAtAction(nameof(GetByRecomendacion), new { recomendacionId = request.RecomendacionId }, ToDto(decision));
    }

    /// <summary>
    /// GET /api/decisionesDespliegue/{id}/acta
    /// Genera el "acta de decisión de despliegue" en PDF: un único documento con
    /// proyecto, versión, métricas, recomendación del sistema y decisión final,
    /// consolidando la trazabilidad completa de esa decisión (RF12/RF13).
    /// </summary>
    [HttpGet("{id}/acta")]
    public async Task<IActionResult> GetActaPdf(int id)
    {
        var decision = await _context.DecisionesDespliegue
            .Include(d => d.UsuarioDecisor)
            .Include(d => d.Recomendacion!).ThenInclude(r => r.Evaluacion!).ThenInclude(e => e.Resultado!).ThenInclude(res => res.Version!).ThenInclude(v => v.Proyecto)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (decision == null || decision.Recomendacion?.Evaluacion?.Resultado?.Version == null)
            return NotFound(new { message = "Decisión no encontrada." });

        var resultado = decision.Recomendacion.Evaluacion.Resultado;
        var version = resultado.Version!;
        var proyecto = version.Proyecto;

        var metricas = await _context.Metricas
            .Where(m => m.ResultadoId == resultado.Id)
            .OrderBy(m => m.NombreMetrica)
            .ToListAsync();

        var acta = new ActaData(
            DecisionId: decision.Id,
            ProyectoNombre: proyecto?.Nombre ?? "—",
            VersionNumero: version.NumeroVersion,
            VersionDescripcion: version.Descripcion,
            ArchivoNombre: resultado.NombreArchivo,
            ArchivoFecha: resultado.FechaCarga,
            Metricas: metricas.Select(m => new ActaMetricaData(
                FormatearNombreMetrica(m.NombreMetrica),
                FormatearValorMetrica(m.ValorMetrica, m.Unidad)
            )).ToList(),
            RecomendacionTipo: decision.Recomendacion.TipoRecomendacion ?? "—",
            RecomendacionJustificacion: decision.Recomendacion.Justificacion,
            RecomendacionFecha: decision.Recomendacion.FechaGeneracion,
            DecisionFinal: decision.DecisionFinal ?? "—",
            Comentario: decision.Comentario ?? "—",
            EsOverride: decision.EsOverride,
            FechaDecision: decision.FechaDecision,
            UsuarioDecisorNombre: decision.UsuarioDecisor != null
                ? $"{decision.UsuarioDecisor.Nombre} {decision.UsuarioDecisor.Apellido}".Trim()
                : "Usuario no identificado"
        );

        var pdfBytes = _actaPdfService.GenerarActaPdf(acta);
        return File(pdfBytes, "application/pdf", $"Acta-Despliegue-{id:D6}.pdf");
    }

    private static string FormatearNombreMetrica(string nombre)
    {
        var n = nombre.Replace('_', ' ');
        return string.IsNullOrEmpty(n) ? n : char.ToUpper(n[0]) + n[1..];
    }

    private static string FormatearValorMetrica(decimal? valor, string? unidad)
    {
        if (valor == null) return "—";
        var u = (unidad ?? "").ToLowerInvariant();
        if (u == "%" || u == "porcentaje" || u == "percent") return $"{valor:0.##}%";
        return string.IsNullOrWhiteSpace(unidad) ? $"{valor:0.###}" : $"{valor:0.###} {unidad}";
    }
}
