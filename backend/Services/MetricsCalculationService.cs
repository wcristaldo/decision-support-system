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
        var version = await _context.Versiones
            .Include(v => v.ResultadosPrueba)
            .FirstOrDefaultAsync(v => v.Id == versionId);

        if (version == null || !version.ResultadosPrueba.Any())
            return;

        var resultados = version.ResultadosPrueba.ToList();

        // Limpiar métricas anteriores
        var metricasAnteriores = await _context.Metricas
            .Where(m => m.VersionId == versionId)
            .ToListAsync();
        _context.Metricas.RemoveRange(metricasAnteriores);

        // Calcular métricas
        var totalPruebas = resultados.Count;
        var pruebasExitosas = resultados.Count(r => r.Estado == "PASSED" || r.Estado == "passed");
        var pruebasFallidas = resultados.Count(r => r.Estado == "FAILED" || r.Estado == "failed");
        var pruebasSkipped = resultados.Count(r => r.Estado == "SKIPPED" || r.Estado == "skipped");

        var tasaExito = (decimal)(pruebasExitosas * 100) / totalPruebas;
        var tasaFallo = (decimal)(pruebasFallidas * 100) / totalPruebas;
        var tiempoPromedio = resultados.Where(r => r.TiempoEjecucion.HasValue)
            .Average(r => r.TiempoEjecucion) ?? 0;

        // Guardar métricas
        var metricas = new List<Metrica>
        {
            new Metrica
            {
                VersionId = versionId,
                NombreMetrica = "tasa_exito",
                Valor = tasaExito,
                Unidad = "%"
            },
            new Metrica
            {
                VersionId = versionId,
                NombreMetrica = "tasa_fallo",
                Valor = tasaFallo,
                Unidad = "%"
            },
            new Metrica
            {
                VersionId = versionId,
                NombreMetrica = "total_pruebas",
                Valor = totalPruebas,
                Unidad = "cantidad"
            },
            new Metrica
            {
                VersionId = versionId,
                NombreMetrica = "pruebas_exitosas",
                Valor = pruebasExitosas,
                Unidad = "cantidad"
            },
            new Metrica
            {
                VersionId = versionId,
                NombreMetrica = "pruebas_fallidas",
                Valor = pruebasFallidas,
                Unidad = "cantidad"
            },
            new Metrica
            {
                VersionId = versionId,
                NombreMetrica = "pruebas_saltadas",
                Valor = pruebasSkipped,
                Unidad = "cantidad"
            },
            new Metrica
            {
                VersionId = versionId,
                NombreMetrica = "tiempo_promedio_ejecucion",
                Valor = (decimal)tiempoPromedio,
                Unidad = "segundos"
            }
        };

        _context.Metricas.AddRange(metricas);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Metrica>> GetMetricsByVersionAsync(int versionId)
    {
        return await _context.Metricas
            .Where(m => m.VersionId == versionId)
            .ToListAsync();
    }
}
