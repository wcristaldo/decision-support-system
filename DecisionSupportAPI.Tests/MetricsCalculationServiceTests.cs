using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de MetricsCalculationService (RF06 — extracción
/// de métricas de ejecución: total, aprobados, fallidos, omitidos, duración,
/// tasa de éxito).</summary>
public class MetricsCalculationServiceTests
{
    private static async Task<(DecisionSupportAPI.Data.ApplicationDbContext ctx, int resultadoId)> ArrangeResultadoAsync()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = new Proyecto { Nombre = "Proyecto Metricas", Estado = "activo" };
        ctx.Proyectos.Add(proyecto);
        await ctx.SaveChangesAsync();
        var version = new Models.Version { ProyectoId = proyecto.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(version);
        await ctx.SaveChangesAsync();
        var resultado = new ResultadoPrueba { VersionId = version.Id, NombreArchivo = "output.json" };
        ctx.ResultadosPrueba.Add(resultado);
        await ctx.SaveChangesAsync();
        return (ctx, resultado.Id);
    }

    [Fact(DisplayName = "CalculateMetricsAsync calcula tasa de éxito y de fallo correctamente")]
    public async Task CalculateMetricsAsync_CalculaTasasDerivadas()
    {
        var (ctx, resultadoId) = await ArrangeResultadoAsync();
        var service = new MetricsCalculationService(ctx);

        await service.CalculateMetricsAsync(resultadoId, totalPruebas: 50, pruebasExitosas: 45, pruebasFallidas: 5, cobertura: 90m, tiempoEjecucion: 12.5m, pruebasOmitidas: 0);

        var metricas = await service.GetMetricsByResultadoAsync(resultadoId);
        Assert.Equal(90m, metricas.First(m => m.NombreMetrica == "tasa_exito").ValorMetrica);
        Assert.Equal(10m, metricas.First(m => m.NombreMetrica == "tasa_fallo").ValorMetrica);
        Assert.Equal(50, metricas.First(m => m.NombreMetrica == "total_pruebas").ValorMetrica);
    }

    [Fact(DisplayName = "CalculateMetricsAsync con total en cero no divide por cero (tasas en 0)")]
    public async Task CalculateMetricsAsync_TotalCero_TasasEnCero()
    {
        var (ctx, resultadoId) = await ArrangeResultadoAsync();
        var service = new MetricsCalculationService(ctx);

        await service.CalculateMetricsAsync(resultadoId, totalPruebas: 0, pruebasExitosas: 0, pruebasFallidas: 0, cobertura: 0, tiempoEjecucion: 0);

        var metricas = await service.GetMetricsByResultadoAsync(resultadoId);
        Assert.Equal(0m, metricas.First(m => m.NombreMetrica == "tasa_exito").ValorMetrica);
        Assert.Equal(0m, metricas.First(m => m.NombreMetrica == "tasa_fallo").ValorMetrica);
    }

    [Fact(DisplayName = "CalculateMetricsAsync recalculado reemplaza (no acumula) las métricas anteriores")]
    public async Task CalculateMetricsAsync_Recalculo_ReemplazaMetricasAnteriores()
    {
        var (ctx, resultadoId) = await ArrangeResultadoAsync();
        var service = new MetricsCalculationService(ctx);

        await service.CalculateMetricsAsync(resultadoId, 50, 40, 10, 80m, 20m);
        await service.CalculateMetricsAsync(resultadoId, 50, 48, 2, 96m, 15m);

        var metricas = await service.GetMetricsByResultadoAsync(resultadoId);
        var porNombre = metricas.Count(m => m.NombreMetrica == "tasa_exito");
        Assert.Equal(1, porNombre);
        Assert.Equal(96m, metricas.First(m => m.NombreMetrica == "tasa_exito").ValorMetrica);
    }

    [Fact(DisplayName = "GetMetricsByVersionAsync devuelve solo las métricas del resultado más reciente")]
    public async Task GetMetricsByVersionAsync_SoloUltimoResultado()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = new Proyecto { Nombre = "P", Estado = "activo" };
        ctx.Proyectos.Add(proyecto);
        await ctx.SaveChangesAsync();
        var version = new Models.Version { ProyectoId = proyecto.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(version);
        await ctx.SaveChangesAsync();

        var service = new MetricsCalculationService(ctx);

        var resultadoViejo = new ResultadoPrueba { VersionId = version.Id, NombreArchivo = "viejo.json", FechaCarga = DateTime.UtcNow.AddDays(-2) };
        ctx.ResultadosPrueba.Add(resultadoViejo);
        await ctx.SaveChangesAsync();
        await service.CalculateMetricsAsync(resultadoViejo.Id, 10, 5, 5, 50m, 5m);

        var resultadoNuevo = new ResultadoPrueba { VersionId = version.Id, NombreArchivo = "nuevo.json", FechaCarga = DateTime.UtcNow };
        ctx.ResultadosPrueba.Add(resultadoNuevo);
        await ctx.SaveChangesAsync();
        await service.CalculateMetricsAsync(resultadoNuevo.Id, 10, 10, 0, 100m, 3m);

        var metricas = await service.GetMetricsByVersionAsync(version.Id);
        Assert.All(metricas, m => Assert.Equal(resultadoNuevo.Id, m.ResultadoId));
    }

    [Fact(DisplayName = "GetMetricsByVersionAsync sin resultados devuelve lista vacía")]
    public async Task GetMetricsByVersionAsync_SinResultados_ListaVacia()
    {
        var ctx = TestHelpers.NewContext();
        var service = new MetricsCalculationService(ctx);

        var metricas = await service.GetMetricsByVersionAsync(9999);

        Assert.Empty(metricas);
    }
}
