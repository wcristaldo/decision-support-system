using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ver_evaluacion")]
public class AnalisisController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ISuscripcionService _suscripcionService;
    private readonly IReporteExportService _export;

    public AnalisisController(ApplicationDbContext context, ISuscripcionService suscripcionService, IReporteExportService export)
    {
        _context = context;
        _suscripcionService = suscripcionService;
        _export = export;
    }

    /// <summary>
    /// GET /api/analisis/historial
    /// Devuelve todas las versiones que tienen al menos una recomendación generada,
    /// con nombre de proyecto, métricas clave y tipo de recomendación.
    /// Ordenado por fecha de evaluación descendente.
    /// </summary>
    [HttpGet("historial")]
    public async Task<ActionResult<List<AnalisisHistorialDto>>> GetHistorial()
        => Ok(await ObtenerHistorialAsync());

    /// <summary>
    /// GET /api/analisis/historial/exportar-pdf
    /// Descarga el historial de análisis (respetando el límite de historial del
    /// plan) como PDF. Requiere que el plan activo tenga ExportarPdf habilitado.
    /// </summary>
    [HttpGet("historial/exportar-pdf")]
    public async Task<IActionResult> ExportarPdf()
    {
        var check = await _suscripcionService.VerificarFeatureAsync(p => p.ExportarPdf, "Exportar PDF");
        if (!check.Permitido)
            return StatusCode(402, new { message = check.Mensaje, codigo = "FEATURE_NO_DISPONIBLE" });

        var historial = await ObtenerHistorialAsync();
        var pdf = _export.GenerarHistorialPdf(historial);
        return File(pdf, "application/pdf", $"roshka-dss-analisis-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf");
    }

    /// <summary>
    /// GET /api/analisis/historial/exportar?formato=csv|xlsx
    /// Descarga el historial de análisis como planilla. Requiere que el plan
    /// activo tenga ExportarExcel habilitado.
    /// </summary>
    [HttpGet("historial/exportar")]
    public async Task<IActionResult> ExportarPlanilla([FromQuery] string formato = "xlsx")
    {
        var check = await _suscripcionService.VerificarFeatureAsync(p => p.ExportarExcel, "Exportar Excel/CSV");
        if (!check.Permitido)
            return StatusCode(402, new { message = check.Mensaje, codigo = "FEATURE_NO_DISPONIBLE" });

        var historial = await ObtenerHistorialAsync();
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");

        if (formato.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = _export.GenerarHistorialCsv(historial);
            return File(csv, "text/csv", $"roshka-dss-analisis-{stamp}.csv");
        }

        var xlsx = _export.GenerarHistorialExcel(historial);
        return File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"roshka-dss-analisis-{stamp}.xlsx");
    }

    private async Task<List<AnalisisHistorialDto>> ObtenerHistorialAsync()
    {
        // Aplicar límite de historial según plan activo
        var plan = await _suscripcionService.GetPlanActivoAsync();
        DateTime? fechaMinima = plan?.HistorialDias.HasValue == true
            ? DateTime.UtcNow.AddDays(-plan.HistorialDias!.Value)
            : null;

        // Carga recomendaciones con la cadena completa: evaluacion → resultado → version + metricas
        var query = _context.Recomendaciones
            .Include(r => r.Evaluacion)
                .ThenInclude(e => e!.Resultado)
                    .ThenInclude(rp => rp!.Version)
            .Include(r => r.Evaluacion)
                .ThenInclude(e => e!.Resultado)
                    .ThenInclude(rp => rp!.Metricas)
            .Include(r => r.Evaluacion)
                .ThenInclude(e => e!.Resultado)
                    .ThenInclude(rp => rp!.UsuarioCarga)
            .AsQueryable();

        if (fechaMinima.HasValue)
            query = query.Where(r => r.FechaGeneracion >= fechaMinima.Value);

        var recomendaciones = await query
            .OrderByDescending(r => r.FechaGeneracion)
            .ToListAsync();

        // Obtener proyectos en una sola consulta para evitar N+1
        var proyectoIds = recomendaciones
            .Select(r => r.Evaluacion?.Resultado?.Version?.ProyectoId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var proyectos = await _context.Proyectos
            .Where(p => proyectoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var resultado = recomendaciones
            .Where(r =>
                r.Evaluacion?.Resultado?.Version != null)
            .Select(r =>
            {
                var rp      = r.Evaluacion!.Resultado!;
                var version = rp.Version!;
                proyectos.TryGetValue(version.ProyectoId, out var proyecto);

                var metricas = rp.Metricas;

                decimal? GetMetrica(string nombre) =>
                    metricas.FirstOrDefault(m => m.NombreMetrica == nombre)?.ValorMetrica;

                return new AnalisisHistorialDto
                {
                    ProyectoId       = version.ProyectoId,
                    ProyectoNombre   = proyecto?.Nombre ?? "—",
                    ProyectoTipo     = proyecto?.TipoSolucion ?? "",
                    VersionId        = version.Id,
                    VersionNumero    = version.NumeroVersion,
                    ResultadoId      = rp.Id,
                    RecomendacionId  = r.Id,
                    Recomendacion    = r.TipoRecomendacion ?? "",
                    Justificacion    = r.Justificacion,
                    FechaCarga       = rp.FechaCarga,
                    FechaEvaluacion  = r.Evaluacion!.FechaEvaluacion,
                    UsuarioCargaNombre = rp.UsuarioCarga != null ? rp.UsuarioCarga.Nombre : null,
                    TasaExito        = GetMetrica("tasa_exito"),
                    TasaFallo        = GetMetrica("tasa_fallo"),
                    Cobertura        = GetMetrica("cobertura"),
                    TiempoEjecucion  = GetMetrica("tiempo_ejecucion"),
                    TotalPruebas     = GetMetrica("total_pruebas"),
                    PruebasExitosas  = GetMetrica("pruebas_exitosas"),
                    PruebasFallidas  = GetMetrica("pruebas_fallidas"),
                };
            })
            .ToList();

        return resultado;
    }
}
