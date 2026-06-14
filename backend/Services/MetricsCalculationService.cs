using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DecisionSupportAPI.Services;

public interface IMetricsCalculationService
{
    Task CalculateMetricsAsync(int versionId);
    Task<List<Metrica>> GetMetricsByVersionAsync(int versionId);
}

public class MetricsCalculationService : IMetricsCalculationService
{
    private readonly ApplicationDbContext _context;

    public MetricsCalculationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task CalculateMetricsAsync(int versionId)
    {
        var resultados = await _context.ResultadosPrueba
            .Where(r => r.VersionId == versionId)
            .ToListAsync();

        if (!resultados.Any())
            return;

        foreach (var resultado in resultados)
        {
            // Eliminar métricas anteriores de este resultado
            var metricasAnteriores = await _context.Metricas
                .Where(m => m.ResultadoId == resultado.Id)
                .ToListAsync();
            _context.Metricas.RemoveRange(metricasAnteriores);

            // Generar métricas basadas en estado de validación
            var metricas = new List<Metrica>
            {
                new Metrica
                {
                    ResultadoId = resultado.Id,
                    NombreMetrica = "archivo_validado",
                    ValorMetrica = resultado.EstadoValidacion == "validado" ? 1 : 0,
                    Unidad = "boolean",
                    FechaCalculo = DateTime.UtcNow
                },
                new Metrica
                {
                    ResultadoId = resultado.Id,
                    NombreMetrica = "estado_validacion",
                    ValorMetrica = resultado.EstadoValidacion switch
                    {
                        "validado"  => 2,
                        "pendiente" => 1,
                        _           => 0  // rechazado
                    },
                    Unidad = "nivel",
                    FechaCalculo = DateTime.UtcNow
                }
            };

            _context.Metricas.AddRange(metricas);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<Metrica>> GetMetricsByVersionAsync(int versionId)
    {
        var resultadoIds = await _context.ResultadosPrueba
            .Where(r => r.VersionId == versionId)
            .Select(r => r.Id)
            .ToListAsync();

        return await _context.Metricas
            .Where(m => resultadoIds.Contains(m.ResultadoId))
            .ToListAsync();
    }
}
