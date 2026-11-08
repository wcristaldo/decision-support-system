using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;

namespace DecisionSupportAPI.Services;

/// <summary>
/// Perfil al que se orienta el reporte ejecutivo en PDF. Cada perfil recibe el mismo
/// resultado de las pruebas, pero explicado con el nivel de detalle que necesita:
/// Gerencia (negocio y riesgo), Técnico (criterio por criterio, para decidir) y
/// Qa (calidad de la ejecución y qué corregir).
/// </summary>
public enum PerfilReporte { Gerencia, Tecnico, Qa }

public record ReporteCriterio(string Nombre, string Criterio, decimal? Valor, decimal? Umbral, string Veredicto, string? Observacion);

public record ReporteHistorialItem(string Version, DateTime Fecha, decimal? TasaExito, decimal? Cobertura, string Recomendacion);

public record ReporteDecision(string Decision, string Comentario, bool EsOverride, DateTime Fecha, string Usuario);

/// <summary>Todos los datos que necesita el PDF, ya resueltos (el PDF no consulta la base).</summary>
public record ReporteEjecutivoData(
    int ResultadoId,
    string Proyecto,
    string? TipoSolucion,
    string Version,
    string? VersionDescripcion,
    string? Archivo,
    DateTime FechaCarga,
    string? CargadoPor,
    string? Observaciones,
    string Recomendacion,                 // DESPLEGAR | REVISAR | NO_DESPLEGAR | SIN_EVALUACION
    string? Justificacion,
    DateTime? FechaEvaluacion,
    int? Total, int? Exitosas, int? Fallidas, int? Omitidas,
    decimal? TasaExito, decimal? TasaFallo, decimal? Cobertura, decimal? Tiempo,
    List<ReporteCriterio> Criterios,
    ReporteDecision? Decision,
    List<ReporteHistorialItem> Historial,  // evaluaciones del proyecto, la más reciente primero (incluye la actual)
    ReporteHistorialItem? Anterior,        // la evaluación inmediatamente anterior del proyecto
    int AdherenciaTotal,
    int AdherenciaAlineadas,
    DetalleRobotFramework? Detalle = null);   // pruebas fallidas con su motivo, más lentas y desglose por módulo

public interface IReporteEjecutivoService
{
    /// <summary>Reúne los datos del reporte de una versión (su último resultado, o el indicado).
    /// Devuelve null si la versión no existe o todavía no tiene resultados cargados.</summary>
    Task<ReporteEjecutivoData?> ObtenerDatosAsync(int versionId, int? resultadoId, CancellationToken ct = default);

    byte[] GenerarPdf(ReporteEjecutivoData datos, PerfilReporte perfil, string solicitante);
}

/// <summary>
/// Reporte ejecutivo por perfil: convierte el resultado técnico de las pruebas automatizadas
/// (métricas, criterios evaluados, recomendación, decisión) en un PDF corto y entendible, distinto
/// para Gerencia, Líder Técnico y Analista QA. A diferencia del acta, no requiere que ya exista una
/// decisión registrada: se puede descargar apenas hay una evaluación.
/// </summary>
public class ReporteEjecutivoService : IReporteEjecutivoService
{
    private readonly ApplicationDbContext _context;

    public ReporteEjecutivoService(ApplicationDbContext context) => _context = context;

    private static string Normalizar(string? tipo) => (tipo ?? "").Trim().ToUpperInvariant() switch
    {
        "DESPLEGAR" => "DESPLEGAR",
        "DESPLEGAR_CON_OBSERVACIONES" or "REVISAR" => "REVISAR",
        "NO_DESPLEGAR" => "NO_DESPLEGAR",
        _ => "SIN_EVALUACION",
    };

    public async Task<ReporteEjecutivoData?> ObtenerDatosAsync(int versionId, int? resultadoId, CancellationToken ct = default)
    {
        var version = await _context.Versiones.Include(v => v.Proyecto).FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (version == null) return null;

        var consulta = _context.ResultadosPrueba
            .Include(r => r.UsuarioCarga)
            .Include(r => r.Metricas)
            .Where(r => r.VersionId == versionId);
        if (resultadoId.HasValue)
            consulta = consulta.Where(r => r.Id == resultadoId.Value);

        var resultado = await consulta.OrderByDescending(r => r.FechaCarga).FirstOrDefaultAsync(ct);
        if (resultado == null) return null;

        decimal? Metrica(string nombre) =>
            resultado.Metricas.FirstOrDefault(m => string.Equals(m.NombreMetrica, nombre, StringComparison.OrdinalIgnoreCase))?.ValorMetrica;
        int? Conteo(string nombre) => Metrica(nombre) is decimal v ? (int)v : null;

        // Última evaluación del resultado, con sus criterios y su recomendación.
        var evaluacion = await _context.Evaluaciones
            .Include(e => e.EvaluacionReglas).ThenInclude(er => er.Regla)
            .Include(e => e.Recomendaciones)
            .Where(e => e.ResultadoId == resultado.Id)
            .OrderByDescending(e => e.FechaEvaluacion)
            .FirstOrDefaultAsync(ct);

        var recomendacion = evaluacion?.Recomendaciones.OrderByDescending(r => r.FechaGeneracion).FirstOrDefault();

        var criterios = new List<ReporteCriterio>();
        foreach (var er in (IEnumerable<Models.EvaluacionRegla>?)evaluacion?.EvaluacionReglas ?? Array.Empty<Models.EvaluacionRegla>())
        {
            var criterio = er.Regla?.Criterio ?? "";
            criterios.Add(new ReporteCriterio(
                Nombre: er.Regla?.Nombre ?? criterio,
                Criterio: criterio,
                Valor: Metrica(criterio),
                Umbral: er.Regla?.Umbral,
                Veredicto: er.ResultadoRegla ?? "no_aplica",
                Observacion: er.Observacion));
        }

        // Orden fijo y lógico (éxito, cobertura, fallo, tiempo); lo que no se reconoce va al final.
        string[] orden = ["tasa_exito", "cobertura", "tasa_fallo", "tiempo_ejecucion"];
        criterios = criterios
            .OrderBy(c => Array.IndexOf(orden, c.Criterio) is var i && i >= 0 ? i : orden.Length)
            .ThenBy(c => c.Nombre)
            .ToList();

        // Decisión final registrada (la más reciente) para ese resultado.
        ReporteDecision? decision = null;
        if (recomendacion != null)
        {
            var d = await _context.DecisionesDespliegue
                .Include(x => x.UsuarioDecisor)
                .Where(x => x.RecomendacionId == recomendacion.Id)
                .OrderByDescending(x => x.FechaDecision)
                .FirstOrDefaultAsync(ct);
            if (d != null)
                decision = new ReporteDecision(
                    d.DecisionFinal ?? "-",
                    d.Comentario ?? "",
                    d.EsOverride,
                    d.FechaDecision,
                    d.UsuarioDecisor != null ? $"{d.UsuarioDecisor.Nombre} {d.UsuarioDecisor.Apellido}".Trim() : "Usuario no identificado");
        }

        // Evolución del proyecto: últimas evaluaciones (de cualquier versión del proyecto).
        var delProyecto = await _context.ResultadosPrueba
            .Include(r => r.Version)
            .Include(r => r.Metricas)
            .Include(r => r.Evaluaciones).ThenInclude(e => e.Recomendaciones)
            .Where(r => r.Version != null && r.Version.ProyectoId == version.ProyectoId)
            .OrderByDescending(r => r.FechaCarga)
            .Take(8)
            .ToListAsync(ct);

        static decimal? Valor(Models.ResultadoPrueba r, string nombre) =>
            r.Metricas.FirstOrDefault(m => string.Equals(m.NombreMetrica, nombre, StringComparison.OrdinalIgnoreCase))?.ValorMetrica;

        var historial = delProyecto.Select(r => new ReporteHistorialItem(
            r.Version?.NumeroVersion ?? "-",
            r.FechaCarga,
            Valor(r, "tasa_exito"),
            Valor(r, "cobertura"),
            Normalizar(r.Evaluaciones.SelectMany(e => e.Recomendaciones).OrderByDescending(x => x.FechaGeneracion).FirstOrDefault()?.TipoRecomendacion)))
            .ToList();

        var anteriorRes = delProyecto.FirstOrDefault(r => r.Id != resultado.Id && r.FechaCarga < resultado.FechaCarga);
        var anterior = anteriorRes == null ? null : historial[delProyecto.IndexOf(anteriorRes)];

        // Adherencia del equipo a la recomendación en este proyecto (decisiones aprobado/rechazado).
        var decisionesProyecto = await _context.DecisionesDespliegue
            .Where(d => d.DecisionFinal == "aprobado" || d.DecisionFinal == "rechazado")
            .Where(d => _context.Recomendaciones
                .Where(r => r.Id == d.RecomendacionId)
                .Join(_context.Evaluaciones, r => r.EvaluacionId, e => e.Id, (r, e) => e.ResultadoId)
                .Join(_context.ResultadosPrueba, resId => resId, res => res.Id, (resId, res) => res.VersionId)
                .Join(_context.Versiones, vId => vId, v => v.Id, (vId, v) => v.ProyectoId)
                .Any(pid => pid == version.ProyectoId))
            .Select(d => d.EsOverride)
            .ToListAsync(ct);

        DetalleRobotFramework? detalle = null;
        try
        {
            detalle = RobotFrameworkDetalle.Deserializar(
                await _context.ResultadosDetalle.Where(x => x.ResultadoId == resultado.Id).Select(x => x.DetalleJson).FirstOrDefaultAsync(ct));
        }
        catch (Exception)
        {
            // la tabla del detalle todavía no existe en esta base: el reporte se genera sin esa sección
        }

        return new ReporteEjecutivoData(
            ResultadoId: resultado.Id,
            Proyecto: version.Proyecto?.Nombre ?? "-",
            TipoSolucion: version.Proyecto?.TipoSolucion,
            Version: version.NumeroVersion,
            VersionDescripcion: version.Descripcion,
            Archivo: resultado.NombreArchivo,
            FechaCarga: resultado.FechaCarga,
            CargadoPor: resultado.UsuarioCarga != null ? $"{resultado.UsuarioCarga.Nombre} {resultado.UsuarioCarga.Apellido}".Trim() : null,
            Observaciones: resultado.Observaciones,
            Recomendacion: Normalizar(recomendacion?.TipoRecomendacion),
            Justificacion: recomendacion?.Justificacion,
            FechaEvaluacion: evaluacion?.FechaEvaluacion,
            Total: Conteo("total_pruebas"), Exitosas: Conteo("pruebas_exitosas"), Fallidas: Conteo("pruebas_fallidas"), Omitidas: Conteo("pruebas_omitidas"),
            TasaExito: Metrica("tasa_exito"), TasaFallo: Metrica("tasa_fallo"), Cobertura: Metrica("cobertura"), Tiempo: Metrica("tiempo_ejecucion"),
            Criterios: criterios,
            Decision: decision,
            Historial: historial,
            Anterior: anterior,
            AdherenciaTotal: decisionesProyecto.Count,
            AdherenciaAlineadas: decisionesProyecto.Count(esOverride => !esOverride),
            Detalle: detalle);
    }

    public byte[] GenerarPdf(ReporteEjecutivoData datos, PerfilReporte perfil, string solicitante)
        => ReporteEjecutivoPdf.Generar(datos, perfil, solicitante);
}
