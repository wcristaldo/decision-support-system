using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Services;
using System.Security.Claims;

namespace DecisionSupportAPI.Controllers;

/// <summary>Formulario de la ingesta automatizada: el archivo output.json y la versión a la que pertenece.</summary>
public class IngestaReporteRequest
{
    [Required(ErrorMessage = "Debe indicar la versión (versionId).")]
    public int? VersionId { get; set; }

    /// <summary>Archivo output.json generado por Robot Framework 7+.</summary>
    public IFormFile? Archivo { get; set; }

    [StringLength(255)]
    public string? Observaciones { get; set; }
}

/// <summary>
/// CU-05 de la tesis: ingesta automatizada de reportes de prueba. Endpoint pensado para que un cliente de la API envíe,
/// de forma automatizada y autenticada, el archivo output.json de cada
/// ejecución de Robot Framework, sin intervención manual del Analista QA (a diferencia de
/// POST /api/ResultadosPrueba, que es la carga manual desde la interfaz web). El backend valida y procesa
/// el archivo (RobotFrameworkParser) y lo pasa al mismo flujo de ingesta que la carga manual
/// (IIngestaResultadosService): límites de plan, cálculo de métricas y recomendación automática.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = "cargar_resultados")]
public class ReportsController : ControllerBase
{
    private const long TamanoMaximoBytes = 10 * 1024 * 1024;
    private readonly IIngestaResultadosService _ingesta;
    private readonly ApplicationDbContext _context;
    private readonly ISuscripcionService _suscripcion;

    public ReportsController(IIngestaResultadosService ingesta, ApplicationDbContext context, ISuscripcionService suscripcion)
    {
        _ingesta = ingesta;
        _context = context;
        _suscripcion = suscripcion;
    }

    /// <summary>
    /// POST /api/reports (multipart/form-data): versionId, archivo (output.json) y observaciones opcionales.
    /// 401 sin token válido (FA-03), 404 si la versión no existe (FA-02), 400 si falta el archivo o no es
    /// .json y 422 si el contenido no es un output.json válido de Robot Framework (FA-01).
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(TamanoMaximoBytes)]
    public async Task<IActionResult> Ingest([FromForm] IngestaReporteRequest request)
    {
        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);

        // La carga automatizada mediante la API es una funcionalidad del plan (Tabla 17);
        // la carga manual desde la interfaz web está disponible en todos los planes.
        var carga = await _suscripcion.VerificarFeatureAsync(p => p.CargaAutomatizadaApi, "Carga automatizada mediante la API");
        if (!carga.Permitido)
            return StatusCode(402, new { message = carga.Mensaje, codigo = "FEATURE_NO_DISPONIBLE" });

        var archivo = request.Archivo;
        if (archivo == null || archivo.Length == 0)
            return BadRequest(new { message = "Debe adjuntar el archivo output.json generado por Robot Framework (campo \"archivo\")." });
        if (!string.Equals(Path.GetExtension(archivo.FileName), ".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "El archivo debe tener extensión .json." });

        string contenido;
        using (var lector = new StreamReader(archivo.OpenReadStream()))
            contenido = await lector.ReadToEndAsync();

        MetricasRobotFramework metricas;
        try
        {
            metricas = RobotFrameworkParser.Parsear(contenido);
        }
        catch (FormatoReporteInvalidoException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }

        var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        int? usuarioCargaId = usuarioIdClaim != null && int.TryParse(usuarioIdClaim.Value, out var uid) ? uid : null;

        var nombreArchivo = Path.GetFileName(archivo.FileName);
        var resultado = await _ingesta.IngestarAsync(new CreateResultadoPruebaDto
        {
            VersionId       = request.VersionId!.Value,
            NombreArchivo   = nombreArchivo.Length > 255 ? nombreArchivo[..255] : nombreArchivo,
            FormatoArchivo  = "JSON",
            Observaciones   = request.Observaciones,
            TotalPruebas    = metricas.TotalPruebas,
            PruebasExitosas = metricas.PruebasExitosas,
            PruebasFallidas = metricas.PruebasFallidas,
            PruebasOmitidas = metricas.PruebasOmitidas,
            Cobertura       = metricas.Cobertura,
            TiempoEjecucion = metricas.TiempoEjecucion,
            TamanoBytes     = archivo.Length,
        }, usuarioCargaId);

        if (resultado.StatusCode != StatusCodes.Status201Created || resultado.Body is not ResultadoPruebaDto creado)
            return StatusCode(resultado.StatusCode, resultado.Body);

        var recomendacion = await _context.Recomendaciones
            .Where(r => r.Evaluacion != null && r.Evaluacion.ResultadoId == creado.Id)
            .OrderByDescending(r => r.FechaGeneracion)
            .Select(r => new { r.TipoRecomendacion, r.Justificacion })
            .FirstOrDefaultAsync();

        return StatusCode(StatusCodes.Status201Created, new
        {
            resultado = creado,
            reporte = new
            {
                herramienta = metricas.Herramienta,
                suite = metricas.NombreSuite,
                estadoSuite = metricas.EstadoSuite,
                totalPruebas = metricas.TotalPruebas,
                pruebasExitosas = metricas.PruebasExitosas,
                pruebasFallidas = metricas.PruebasFallidas,
                pruebasOmitidas = metricas.PruebasOmitidas,
                tiempoEjecucionSegundos = metricas.TiempoEjecucion,
            },
            recomendacion = recomendacion?.TipoRecomendacion,
            justificacion = recomendacion?.Justificacion,
        });
    }
}
