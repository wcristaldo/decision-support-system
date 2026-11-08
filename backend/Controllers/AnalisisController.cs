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
    private readonly IProyectoAccesoService _acceso;
    private readonly IReporteEjecutivoService _reporteEjecutivo;

    public AnalisisController(ApplicationDbContext context, ISuscripcionService suscripcionService, IReporteExportService export, IProyectoAccesoService acceso, IReporteEjecutivoService reporteEjecutivo)
    {
        _context = context;
        _suscripcionService = suscripcionService;
        _export = export;
        _acceso = acceso;
        _reporteEjecutivo = reporteEjecutivo;
    }

    /// <summary>
    /// GET /api/analisis/version/{versionId}/reporte-ejecutivo?perfil=gerencia|tecnico|qa&amp;resultadoId=
    /// Reporte ejecutivo en PDF de una versión, orientado al perfil del usuario: convierte el resultado
    /// técnico de las pruebas automatizadas en un documento corto y entendible. Está disponible apenas hay
    /// una evaluación (no hace falta una decisión registrada, a diferencia del acta). Sin "perfil" se elige
    /// el que corresponde al rol; Gerencia es solo para Gerente QA/Administrador y Técnico para Líder
    /// Técnico/Administrador (los mismos que ya ven esas secciones en pantalla); QA es para cualquiera
    /// con acceso al análisis. Requiere que el plan activo tenga ExportarPdf habilitado.
    /// </summary>
    [HttpGet("version/{versionId:int}/reporte-ejecutivo")]
    public async Task<IActionResult> ReporteEjecutivo(int versionId, [FromQuery] string? perfil = null, [FromQuery] int? resultadoId = null, CancellationToken ct = default)
    {
        var check = await _suscripcionService.VerificarFeatureAsync(p => p.ExportarPdf, "Exportar PDF");
        if (!check.Permitido)
            return StatusCode(402, new { message = check.Mensaje, codigo = "FEATURE_NO_DISPONIBLE" });

        PerfilReporte elegido;
        if (string.IsNullOrWhiteSpace(perfil))
            elegido = User.IsInRole("Administrador") || User.IsInRole("Gerente QA") ? PerfilReporte.Gerencia
                    : User.IsInRole("Líder Técnico") ? PerfilReporte.Tecnico
                    : PerfilReporte.Qa;
        else if (!Enum.TryParse(perfil.Trim().Replace("é", "e"), ignoreCase: true, out elegido) || !Enum.IsDefined(elegido))
            return BadRequest(new { message = "perfil debe ser 'gerencia', 'tecnico' o 'qa'." });

        var admin = User.IsInRole("Administrador");
        var permitido = elegido switch
        {
            PerfilReporte.Gerencia => admin || User.IsInRole("Gerente QA"),
            PerfilReporte.Tecnico => admin || User.IsInRole("Líder Técnico"),
            _ => true,
        };
        if (!permitido)
            return StatusCode(403, new { message = "Tu rol no tiene disponible ese reporte." });

        if (!await _acceso.TieneAccesoAVersionAsync(User, versionId)) return Forbid();

        var datos = await _reporteEjecutivo.ObtenerDatosAsync(versionId, resultadoId, ct);
        if (datos == null)
            return NotFound(new { message = "La versión no existe o todavía no tiene resultados de pruebas cargados." });

        var solicitante = "Usuario";
        if (int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid))
        {
            var u = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.IdUsuario == uid, ct);
            if (u != null) solicitante = $"{u.Nombre} {u.Apellido}".Trim();
        }

        var pdf = _reporteEjecutivo.GenerarPdf(datos, elegido, solicitante);
        return File(pdf, "application/pdf", $"Reporte-Ejecutivo-{elegido}-RPT{datos.ResultadoId:D6}.pdf");
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
    /// plan y los filtros activos de la pantalla) como PDF. Requiere que el plan
    /// activo tenga ExportarPdf habilitado.
    /// </summary>
    [HttpGet("historial/exportar-pdf")]
    public async Task<IActionResult> ExportarPdf([FromQuery] HistorialFiltroDto? filtro = null)
    {
        var check = await _suscripcionService.VerificarFeatureAsync(p => p.ExportarPdf, "Exportar PDF");
        if (!check.Permitido)
            return StatusCode(402, new { message = check.Mensaje, codigo = "FEATURE_NO_DISPONIBLE" });

        var historial = Filtrar(await ObtenerHistorialAsync(), filtro);
        var pdf = _export.GenerarHistorialPdf(historial);
        return File(pdf, "application/pdf", $"roshka-dss-analisis-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf");
    }

    /// <summary>
    /// GET /api/analisis/historial/exportar?formato=csv|xlsx
    /// Descarga el historial de análisis (con los filtros activos de la pantalla)
    /// como planilla. Requiere que el plan activo tenga ExportarExcel habilitado.
    /// </summary>
    [HttpGet("historial/exportar")]
    public async Task<IActionResult> ExportarPlanilla([FromQuery] string formato = "xlsx", [FromQuery] HistorialFiltroDto? filtro = null)
    {
        var check = await _suscripcionService.VerificarFeatureAsync(p => p.ExportarExcel, "Exportar Excel/CSV");
        if (!check.Permitido)
            return StatusCode(402, new { message = check.Mensaje, codigo = "FEATURE_NO_DISPONIBLE" });

        var historial = Filtrar(await ObtenerHistorialAsync(), filtro);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");

        if (formato.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = _export.GenerarHistorialCsv(historial);
            return File(csv, "text/csv", $"roshka-dss-analisis-{stamp}.csv");
        }

        var xlsx = _export.GenerarHistorialExcel(historial);
        return File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"roshka-dss-analisis-{stamp}.xlsx");
    }

    /// <summary>
    /// Aplica a la lista los mismos criterios con los que la pantalla filtra el
    /// historial (estado, proyecto, responsable y rango de fecha de carga), para
    /// que lo exportado coincida con lo que el usuario ve.
    /// </summary>
    private static List<AnalisisHistorialDto> Filtrar(List<AnalisisHistorialDto> historial, HistorialFiltroDto? filtro)
    {
        if (filtro is null)
            return historial;

        static string NormalizarRec(string? t)
        {
            var u = (t ?? "").Trim().ToUpperInvariant();
            return u == "DESPLEGAR_CON_OBSERVACIONES" ? "REVISAR" : u;
        }

        var rec = NormalizarRec(filtro.Recomendacion);
        var filtrarRec = rec != "" && rec != "TODOS";

        return historial.Where(h =>
        {
            if (filtrarRec && NormalizarRec(h.Recomendacion) != rec) return false;
            if (!string.IsNullOrWhiteSpace(filtro.Proyecto) && h.ProyectoNombre != filtro.Proyecto) return false;
            if (!string.IsNullOrWhiteSpace(filtro.Responsable) && h.UsuarioCargaNombre != filtro.Responsable) return false;
            var dia = h.FechaCarga.ToString("yyyy-MM-dd");
            if (!string.IsNullOrWhiteSpace(filtro.Desde) && string.CompareOrdinal(dia, filtro.Desde) < 0) return false;
            if (!string.IsNullOrWhiteSpace(filtro.Hasta) && string.CompareOrdinal(dia, filtro.Hasta) > 0) return false;
            return true;
        }).ToList();
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

        // RF13: un usuario sin acceso irrestricto solo ve el historial de SUS
        // proyectos asignados.
        if (!_acceso.EsIrrestricto(User))
        {
            var permitidos = _acceso.ProyectosPermitidos(User);
            recomendaciones = recomendaciones
                .Where(r => r.Evaluacion?.Resultado?.Version != null
                    && permitidos.Contains(r.Evaluacion.Resultado.Version.ProyectoId))
                .ToList();
        }

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
                    ProyectoNombre   = proyecto?.Nombre ?? "-",
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
