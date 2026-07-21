using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DecisionSupportAPI.Services;

public interface IMetricsCalculationService
{
    /// <summary>
    /// Calcula y persiste las métricas reales de un resultado de prueba.
    /// Los valores provienen directamente del reporte cargado por el usuario.
    /// </summary>
    Task CalculateMetricsAsync(
        int resultadoId,
        int totalPruebas,
        int pruebasExitosas,
        int pruebasFallidas,
        decimal cobertura,
        decimal tiempoEjecucion);

    Task<List<Metrica>> GetMetricsByVersionAsync(int versionId);
}

public class MetricsCalculationService : IMetricsCalculationService
{
    private readonly ApplicationDbContext _context;

    public MetricsCalculationService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task CalculateMetricsAsync(
        int resultadoId,
        int totalPruebas,
        int pruebasExitosas,
        int pruebasFallidas,
        decimal cobertura,
        decimal tiempoEjecucion)
    {
        // Eliminar métricas anteriores de este resultado (en caso de recálculo)
        var anteriores = await _context.Metricas
            .Where(m => m.ResultadoId == resultadoId)
            .ToListAsync();
        _context.Metricas.RemoveRange(anteriores);

        // Calcular tasas derivadas
        decimal tasaExito = totalPruebas > 0
            ? Math.Round((decimal)pruebasExitosas / totalPruebas * 100, 2)
            : 0;

        decimal tasaFallo = totalPruebas > 0
            ? Math.Round((decimal)pruebasFallidas / totalPruebas * 100, 2)
            : 0;

        // Persistir las 7 métricas definidas en la tesis (Tablas 24 y 20)
        var metricas = new List<Metrica>
        {
            new() { ResultadoId = resultadoId, NombreMetrica = "total_pruebas",    ValorMetrica = totalPruebas,    Unidad = "casos",    FechaCalculo = DateTime.UtcNow },
            new() { ResultadoId = resultadoId, NombreMetrica = "pruebas_exitosas", ValorMetrica = pruebasExitosas, Unidad = "casos",    FechaCalculo = DateTime.UtcNow },
            new() { ResultadoId = resultadoId, NombreMetrica = "pruebas_fallidas", ValorMetrica = pruebasFallidas, Unidad = "casos",    FechaCalculo = DateTime.UtcNow },
            new() { ResultadoId = resultadoId, NombreMetrica = "tasa_exito",       ValorMetrica = tasaExito,       Unidad = "%",        FechaCalculo = DateTime.UtcNow },
            new() { ResultadoId = resultadoId, NombreMetrica = "tasa_fallo",       ValorMetrica = tasaFallo,       Unidad = "%",        FechaCalculo = DateTime.UtcNow },
            new() { ResultadoId = resultadoId, NombreMetrica = "cobertura",        ValorMetrica = cobertura,       Unidad = "%",        FechaCalculo = DateTime.UtcNow },
            new() { ResultadoId = resultadoId, NombreMetrica = "tiempo_ejecucion", ValorMetrica = tiempoEjecucion, Unidad = "segundos", FechaCalculo = DateTime.UtcNow },
        };

        _context.Metricas.AddRange(metricas);
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
            .OrderBy(m => m.NombreMetrica)
            .ToListAsync();
    }
}
