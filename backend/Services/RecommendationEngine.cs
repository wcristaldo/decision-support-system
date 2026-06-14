using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DecisionSupportAPI.Services;

public interface IRecommendationEngine
{
    Task GenerateRecommendationsAsync(int versionId);
    Task<List<Recomendacion>> GetRecommendationsByVersionAsync(int versionId);
}

public class RecommendationEngine : IRecommendationEngine
{
    private readonly ApplicationDbContext _context;

    public RecommendationEngine(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task GenerateRecommendationsAsync(int versionId)
    {
        // Obtener evaluaciones asociadas a los resultados de esta versión
        var resultadoIds = await _context.ResultadosPrueba
            .Where(r => r.VersionId == versionId)
            .Select(r => r.Id)
            .ToListAsync();

        if (!resultadoIds.Any())
            return;

        var evaluaciones = await _context.Evaluaciones
            .Where(e => resultadoIds.Contains(e.ResultadoId))
            .ToListAsync();

        foreach (var evaluacion in evaluaciones)
        {
            // Eliminar recomendaciones anteriores
            var anteriores = await _context.Recomendaciones
                .Where(r => r.EvaluacionId == evaluacion.Id)
                .ToListAsync();
            _context.Recomendaciones.RemoveRange(anteriores);

            // Generar recomendación basada en el estado de la evaluación
            var tipoRecomendacion = evaluacion.EstadoEvaluacion switch
            {
                "aprobado"  => "DESPLEGAR",
                "pendiente" => "REVISAR",
                _           => "NO_DESPLEGAR"
            };

            var justificacion = evaluacion.EstadoEvaluacion switch
            {
                "aprobado"  => "La evaluación fue aprobada. El sistema está listo para despliegue.",
                "pendiente" => "La evaluación está pendiente. Se recomienda revisión antes de desplegar.",
                _           => "La evaluación no fue aprobada. No se recomienda desplegar hasta resolver los problemas."
            };

            _context.Recomendaciones.Add(new Recomendacion
            {
                EvaluacionId = evaluacion.Id,
                TipoRecomendacion = tipoRecomendacion,
                Justificacion = evaluacion.ResumenEvaluacion ?? justificacion,
                FechaGeneracion = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
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
            .ToListAsync();
    }
}
