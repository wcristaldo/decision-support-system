using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;
using Xunit;

namespace DecisionSupportAPI.Tests;

/// <summary>
/// Casos de prueba unitarios CP-U01 a CP-U04 de la Tabla 21 de la tesis, ejecutados
/// contra el motor de recomendación real (RecommendationEngine), con una base
/// InMemory de Entity Framework Core para no depender de PostgreSQL.
/// </summary>
public class RecommendationEngineTests
{
    private static ApplicationDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    /// <summary>Prepara un proyecto/versión/resultado con las métricas y reglas
    /// dadas, y devuelve el id del resultado creado.</summary>
    private static async Task<(ApplicationDbContext ctx, int resultadoId)> ArrangeAsync(
        Dictionary<string, decimal> metricas,
        Dictionary<string, decimal> umbrales)
    {
        var ctx = NewContext();

        var proyecto = new Proyecto { Nombre = "Proyecto de prueba", Estado = "activo" };
        ctx.Proyectos.Add(proyecto);
        await ctx.SaveChangesAsync();

        var version = new Models.Version { ProyectoId = proyecto.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(version);
        await ctx.SaveChangesAsync();

        var resultado = new ResultadoPrueba { VersionId = version.Id, NombreArchivo = "output.json" };
        ctx.ResultadosPrueba.Add(resultado);
        await ctx.SaveChangesAsync();

        foreach (var (criterio, valor) in metricas)
            ctx.Metricas.Add(new Metrica { ResultadoId = resultado.Id, NombreMetrica = criterio, ValorMetrica = valor });

        foreach (var (criterio, umbral) in umbrales)
            ctx.ReglasEvaluacion.Add(new ReglaEvaluacion
            {
                Nombre = $"Regla {criterio}",
                Criterio = criterio,
                Umbral = umbral,
                Estado = "activo",
            });

        await ctx.SaveChangesAsync();
        return (ctx, resultado.Id);
    }

    private static async Task<string?> GenerarYObtenerTipoAsync(ApplicationDbContext ctx, int resultadoId)
    {
        var engine = new RecommendationEngine(ctx);
        await engine.GenerateRecommendationForResultadoAsync(resultadoId);

        return await ctx.Recomendaciones
            .Include(r => r.Evaluacion)
            .Where(r => r.Evaluacion!.ResultadoId == resultadoId)
            .Select(r => r.TipoRecomendacion)
            .FirstOrDefaultAsync();
    }

    [Fact(DisplayName = "CP-U01: Recomendación Apto cuando todas las métricas superan los umbrales")]
    public async Task CP_U01_TodasLasMetricasSuperanUmbral_Desplegar()
    {
        var (ctx, resultadoId) = await ArrangeAsync(
            metricas: new() { ["cobertura"] = 85m, ["tasa_exito"] = 95m, ["tiempo_ejecucion"] = 45m },
            umbrales: new() { ["cobertura"] = 80m, ["tasa_exito"] = 90m, ["tiempo_ejecucion"] = 120m });

        var tipo = await GenerarYObtenerTipoAsync(ctx, resultadoId);

        Assert.Equal("desplegar", tipo);
    }

    [Fact(DisplayName = "CP-U02: Recomendación No Apto por cobertura insuficiente")]
    public async Task CP_U02_CoberturaInsuficiente_NoDesplegar()
    {
        var (ctx, resultadoId) = await ArrangeAsync(
            metricas: new() { ["cobertura"] = 65m, ["tasa_exito"] = 98m, ["tiempo_ejecucion"] = 30m },
            umbrales: new() { ["cobertura"] = 80m });

        var tipo = await GenerarYObtenerTipoAsync(ctx, resultadoId);

        Assert.Equal("no_desplegar", tipo);
    }

    [Fact(DisplayName = "CP-U03: Recomendación No Apto por tasa de éxito insuficiente")]
    public async Task CP_U03_TasaExitoInsuficiente_NoDesplegar()
    {
        var (ctx, resultadoId) = await ArrangeAsync(
            metricas: new() { ["cobertura"] = 90m, ["tasa_exito"] = 75m, ["tiempo_ejecucion"] = 30m },
            umbrales: new() { ["tasa_exito"] = 90m });

        var tipo = await GenerarYObtenerTipoAsync(ctx, resultadoId);

        Assert.Equal("no_desplegar", tipo);
    }

    [Fact(DisplayName = "CP-U04: Recomendación Revisar cuando las métricas están dentro del margen de tolerancia (±5%)")]
    public async Task CP_U04_DentroDeTolerancia_Revisar()
    {
        var (ctx, resultadoId) = await ArrangeAsync(
            metricas: new() { ["cobertura"] = 77m, ["tasa_exito"] = 87m },
            umbrales: new() { ["cobertura"] = 80m, ["tasa_exito"] = 90m });

        var tipo = await GenerarYObtenerTipoAsync(ctx, resultadoId);

        Assert.Equal("desplegar_con_observaciones", tipo);
    }
}

/// <summary>
/// CP-U05 de la Tabla 21: verifica que el hash de contraseñas use BCrypt
/// (RNF04) y no el SHA-256 plano que usaba el sistema originalmente.
/// </summary>
public class AuthenticationServiceTests
{
    private static AuthenticationService NewService()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new ApplicationDbContext(options);
        var config = new ConfigurationBuilder().Build();
        return new AuthenticationService(ctx, config);
    }

    [Fact(DisplayName = "CP-U05: Hash BCrypt generado correctamente para contraseña")]
    public void CP_U05_HashBcryptConPrefijoValido()
    {
        var auth = NewService();

        var hash = auth.HashPassword("Test@1234");

        Assert.StartsWith("$2", hash); // BCrypt.Net-Next produce prefijo $2a$/$2b$
        Assert.True(auth.VerifyPassword("Test@1234", hash));
        Assert.False(auth.VerifyPassword("otra-password", hash));
    }
}
