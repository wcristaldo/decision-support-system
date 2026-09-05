using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Services;
using System.Security.Claims;

namespace DecisionSupportAPI.Controllers;

/// <summary>
/// CU-05 de la tesis: "Ingestar reporte de prueba CI/CD". Endpoint pensado para
/// que un pipeline de integración continua (GitHub Actions, GitLab CI, etc.)
/// envíe el reporte de una ejecución de forma automatizada y autenticada,
/// sin intervención manual del Analista QA — a diferencia de
/// POST /api/ResultadosPrueba, que es la carga manual desde la interfaz web.
/// Ambos comparten el mismo pipeline de ingesta (IIngestaResultadosService):
/// validación de límites de plan, cálculo de métricas y generación automática
/// de la recomendación de despliegue.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = "cargar_resultados")]
public class ReportsController : ControllerBase
{
    private readonly IIngestaResultadosService _ingesta;

    public ReportsController(IIngestaResultadosService ingesta)
    {
        _ingesta = ingesta;
    }

    /// <summary>
    /// POST /api/reports
    /// Body: mismo contrato que POST /api/ResultadosPrueba (CreateResultadoPruebaDto).
    /// Requiere token JWT válido con permiso "cargar_resultados" (FA-03: 401 si no
    /// hay token válido). Devuelve 404 si la versión no existe (FA-02) y 400/422
    /// si el cuerpo es inválido (FA-01).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Ingest([FromBody] CreateResultadoPruebaDto request)
    {
        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);

        var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        int? usuarioCargaId = usuarioIdClaim != null && int.TryParse(usuarioIdClaim.Value, out var uid) ? uid : null;

        var resultado = await _ingesta.IngestarAsync(request, usuarioCargaId);
        return StatusCode(resultado.StatusCode, resultado.Body);
    }
}
