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
    private readonly IMetricsCalculationService _metricsService;

    public RecommendationEngine(ApplicationDbContext context, IMetricsCalculationService metricsService)
    {
        _context = context;
        _metricsService = metricsService;
    }

    public async Task GenerateRecommendationsAsync(int versionId)
    {
        var version = await _context.Versiones
            .Include(v => v.Metricas)
            .FirstOrDefaultAsync(v => v.Id == versionId);

        if (version == null)
            return;

        // Limpiar recomendaciones anteriores
        var recomendacionesAnteriores = await _context.Recomendaciones
            .Where(r => r.VersionId == versionId)
            .ToListAsync();
        _context.Recomendaciones.RemoveRange(recomendacionesAnteriores);

        var metricas = version.Metricas;
        var tasaExito = metricas.FirstOrDefault(m => m.NombreMetrica == "tasa_exito")?.Valor ?? 0;
        var tasaFallo = metricas.FirstOrDefault(m => m.NombreMetrica == "tasa_fallo")?.Valor ?? 0;

        var recomendaciones = new List<Recomendacion>();

        if (tasaExito >= 95)
        {
            recomendaciones.Add(new Recomendacion
            {
                VersionId = versionId,
                TipoRecomendacion = "DESPLEGAR",
                Descripcion = "Tasa de éxito superior al 95%. Sistema está en excelente condición para desplegar.",
                Confianza = 100
            });
        }
        else if (tasaExito >= 80 && tasaExito < 95)
        {
            recomendaciones.Add(new Recomendacion
            {
                VersionId = versionId,
                TipoRecomendacion = "DESPLEGAR_CON_MONITOREO",
                Descripcion = "Tasa de éxito entre 80-95%. Desplegar con monitoreo especial.",
                Confianza = 85
            });
        }
        else if (tasaExito >= 70 && tasaExito < 80)
        {
            recomendaciones.Add(new Recomendacion
            {
                VersionId = versionId,
                TipoRecomendacion = "INVESTIGAR",
                Descripcion = "Tasa de éxito entre 70-80%. Se recomienda investigar las fallas antes de desplegar.",
                Confianza = 70
            });
        }
        else
        {
            recomendaciones.Add(new Recomendacion
            {
                VersionId = versionId,
                TipoRecomendacion = "NO_DESPLEGAR",
                Descripcion = $"Tasa de éxito baja ({tasaExito:F2}%). Se recomienda NO desplegar hasta resolver los problemas.",
                Confianza = 90
            });
        }

        // Alertas de fallas críticas
        if (tasaFallo > 20)
        {
            recomendaciones.Add(new Recomendacion
            {
                VersionId = versionId,
                TipoRecomendacion = "ALERTA_CRITICA",
                Descripcion = $"Más del 20% de las pruebas están fallando ({tasaFallo:F2}%). Hay problemas críticos.",
                Confianza = 95
            });
        }

        _context.Recomendaciones.AddRange(recomendaciones);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Recomendacion>> GetRecommendationsByVersionAsync(int versionId)
    {
        return await _context.Recomendaciones
            .Where(r => r.VersionId == versionId)
            .ToListAsync();
    }
}
