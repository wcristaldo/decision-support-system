using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DecisionSupportAPI.Services;

public interface IRecommendationEngine
{
    Task GenerateRecommendationsAsync(int versionId);
    Task<List<Recomendacion>> GetRecommendationsByVersionAsync(int versionId);
}

/// <summary>
/// Motor de recomendación automática de despliegue.
/// Implementa la lógica definida en la tesis (modelo de toma de decisiones,
/// Capítulo II) y los casos de prueba CP-U01 a CP-U04:
///
///   - "mayor_igual": métricas cuyo valor debe ser ≥ umbral (tasa_exito, cobertura)
///   - "menor_igual": métricas cuyo valor debe ser ≤ umbral (tasa_fallo, tiempo_ejecucion)
///
/// Tolerancia ±5 %:
///   - Para "mayor_igual": valor ≥ umbral*0.95  → REVISAR  (CP-U04: cov=77%, min=80%)
///   - Para "menor_igual": valor ≤ umbral*1.05  → REVISAR
///
/// Resultado final:
///   - Sin ningún no_cumple → DESPLEGAR
///   - Con algún no_cumple  → NO_DESPLEGAR
///   - Sin no_cumple pero con algún revisar → REVISAR
/// </summary>
public class RecommendationEngine : IRecommendationEngine
{
    private readonly ApplicationDbContext _context;

    // Reglas cuyo criterio se evalúa como "mayor o igual" al umbral
    private static readonly HashSet<string> CriteriosMayorIgual = new(StringComparer.OrdinalIgnoreCase)
    {
        "tasa_exito", "cobertura"
    };

    // Reglas cuyo criterio se evalúa como "menor o igual" al umbral
    private static readonly HashSet<string> CriteriosMenorIgual = new(StringComparer.OrdinalIgnoreCase)
    {
        "tasa_fallo", "tiempo_ejecucion"
    };

    private const decimal Tolerancia = 0.05m; // ±5 %

    public RecommendationEngine(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task GenerateRecommendationsAsync(int versionId)
    {
        // 1. Obtener todos los resultados de prueba de la versión
        var resultados = await _context.ResultadosPrueba
            .Where(r => r.VersionId == versionId)
            .ToListAsync();

        if (!resultados.Any()) return;

        // 2. Cargar las reglas de evaluación activas
        var reglas = await _context.ReglasEvaluacion
            .Where(r => r.Estado == "activo")
            .ToListAsync();

        if (!reglas.Any()) return;

        // 3. Procesar cada resultado de prueba
        foreach (var resultado in resultados)
        {
            // 3a. Obtener las métricas calculadas para este resultado
            var metricas = await _context.Metricas
                .Where(m => m.ResultadoId == resultado.Id)
                .ToListAsync();

            if (!metricas.Any()) continue;

            var metricaDict = metricas
                .GroupBy(m => m.NombreMetrica, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().ValorMetrica ?? 0, StringComparer.OrdinalIgnoreCase);

            // 3b. Limpiar evaluaciones y recomendaciones anteriores
            var evsAnteriores = await _context.Evaluaciones
                .Include(e => e.EvaluacionReglas)
                .Include(e => e.Recomendaciones)
                .Where(e => e.ResultadoId == resultado.Id)
                .ToListAsync();

            foreach (var ev in evsAnteriores)
            {
                _context.EvaluacionReglas.RemoveRange(ev.EvaluacionReglas);
                _context.Recomendaciones.RemoveRange(ev.Recomendaciones);
            }
            _context.Evaluaciones.RemoveRange(evsAnteriores);
            await _context.SaveChangesAsync();

            // 3c. Crear nueva evaluación
            var evaluacion = new Evaluacion
            {
                ResultadoId      = resultado.Id,
                FechaEvaluacion  = DateTime.UtcNow,
                EstadoEvaluacion = "en_proceso",
            };
            _context.Evaluaciones.Add(evaluacion);
            await _context.SaveChangesAsync();

            // 3d. Evaluar cada regla contra las métricas
            bool hayNoCumple = false;
            bool hayRevisar  = false;
            var detalles     = new List<string>();

            foreach (var regla in reglas)
            {
                if (!metricaDict.TryGetValue(regla.Criterio ?? "", out var valor))
                    continue; // métrica no disponible para esta regla

                var umbral = regla.Umbral ?? 0;
                string resultadoRegla;
                string observacion;

                if (CriteriosMayorIgual.Contains(regla.Criterio ?? ""))
                {
                    if (valor >= umbral)
                    {
                        resultadoRegla = "cumple";
                        observacion    = $"{regla.Criterio}: {valor:F2} ≥ {umbral:F2} (umbral)";
                    }
                    else if (valor >= umbral * (1 - Tolerancia))
                    {
                        resultadoRegla = "revisar";
                        observacion    = $"{regla.Criterio}: {valor:F2} está dentro del margen de tolerancia (umbral: {umbral:F2})";
                        hayRevisar     = true;
                    }
                    else
                    {
                        resultadoRegla = "no_cumple";
                        observacion    = $"{regla.Criterio}: {valor:F2} < {umbral:F2} (umbral)";
                        hayNoCumple    = true;
                    }
                }
                else if (CriteriosMenorIgual.Contains(regla.Criterio ?? ""))
                {
                    if (valor <= umbral)
                    {
                        resultadoRegla = "cumple";
                        observacion    = $"{regla.Criterio}: {valor:F2} ≤ {umbral:F2} (umbral)";
                    }
                    else if (valor <= umbral * (1 + Tolerancia))
                    {
                        resultadoRegla = "revisar";
                        observacion    = $"{regla.Criterio}: {valor:F2} está dentro del margen de tolerancia (umbral: {umbral:F2})";
                        hayRevisar     = true;
                    }
                    else
                    {
                        resultadoRegla = "no_cumple";
                        observacion    = $"{regla.Criterio}: {valor:F2} > {umbral:F2} (umbral)";
                        hayNoCumple    = true;
                    }
                }
                else
                {
                    // Criterio no reconocido → no aplica
                    resultadoRegla = "no_aplica";
                    observacion    = $"Criterio '{regla.Criterio}' no disponible en este resultado.";
                }

                _context.EvaluacionReglas.Add(new EvaluacionRegla
                {
                    EvaluacionId   = evaluacion.Id,
                    ReglaId        = regla.Id,
                    ResultadoRegla = resultadoRegla,
                    Observacion    = observacion,
                });

                if (resultadoRegla != "no_aplica")
                    detalles.Add(observacion);
            }

            // 3e. Determinar tipo de recomendación
            string tipoRecomendacion;
            string resumen;

            if (hayNoCumple)
            {
                tipoRecomendacion = "NO_DESPLEGAR";
                resumen           = "No apto para despliegue: una o más métricas están por debajo del umbral mínimo requerido.";
                evaluacion.EstadoEvaluacion = "completada";
            }
            else if (hayRevisar)
            {
                tipoRecomendacion = "REVISAR";
                resumen           = "Requiere revisión: algunas métricas están dentro del margen de tolerancia (±5%) pero no superan el umbral.";
                evaluacion.EstadoEvaluacion = "completada";
            }
            else
            {
                tipoRecomendacion = "DESPLEGAR";
                resumen           = "Apto para despliegue: todas las métricas cumplen los umbrales de calidad establecidos.";
                evaluacion.EstadoEvaluacion = "completada";
            }

            evaluacion.ResumenEvaluacion = resumen;
            _context.Evaluaciones.Update(evaluacion);

            _context.Recomendaciones.Add(new Recomendacion
            {
                EvaluacionId      = evaluacion.Id,
                TipoRecomendacion = tipoRecomendacion,
                Justificacion     = resumen + " Detalle: " + string.Join(" | ", detalles),
                FechaGeneracion   = DateTime.UtcNow,
            });

            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Recomendacion>> GetRecommendationsByVersionAsync(int versionId)
    {
        var resultadoIds = await _context.ResultadosPrueba
            .Where(r => r.VersionId == versionId)
            .Select(r => r.Id)
            .ToListAsync();

        var evaluacionIds = await _context.Evaluaciones
            .Where(e => resultadoIds.Contains(e.ResultadoId))
            .Select(e => e.Id)
            .ToListAsync();

        return await _context.Recomendaciones
            .Where(r => evaluacionIds.Contains(r.EvaluacionId))
            .OrderByDescending(r => r.FechaGeneracion)
            .ToListAsync();
    }
}
