using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de RecomendacionesController (RF09, RF13).</summary>
public class RecomendacionesControllerTests
{
    private static RecomendacionesController NewController(ApplicationDbContext ctx, System.Security.Claims.ClaimsPrincipal user)
    {
        var controller = new RecomendacionesController(new RecommendationEngine(ctx), ctx, new ProyectoAccesoService(ctx));
        TestHelpers.SetUser(controller, user);
        return controller;
    }

    private static async Task<(Models.Version version, int resultadoId)> SeedConReglaAsync(ApplicationDbContext ctx)
    {
        var p = new Proyecto { Nombre = "P", Estado = "activo" };
        ctx.Proyectos.Add(p);
        await ctx.SaveChangesAsync();
        var v = new Models.Version { ProyectoId = p.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(v);
        await ctx.SaveChangesAsync();
        var r = new ResultadoPrueba { VersionId = v.Id, NombreArchivo = "a.json" };
        ctx.ResultadosPrueba.Add(r);
        await ctx.SaveChangesAsync();
        ctx.Metricas.Add(new Metrica { ResultadoId = r.Id, NombreMetrica = "tasa_exito", ValorMetrica = 95m });
        ctx.ReglasEvaluacion.Add(new ReglaEvaluacion { Nombre = "Tasa éxito", Criterio = "tasa_exito", Umbral = 90m, Estado = "activo" });
        await ctx.SaveChangesAsync();
        return (v, r.Id);
    }

    [Fact(DisplayName = "Generate crea una recomendación 'desplegar' cuando la métrica supera el umbral")]
    public async Task Generate_MetricaSuperaUmbral_GeneraDesplegar()
    {
        var ctx = TestHelpers.NewContext();
        var (version, _) = await SeedConReglaAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ejecutar_evaluacion" });
        var controller = NewController(ctx, admin);

        var result = await controller.Generate(version.Id);

        Assert.IsType<OkObjectResult>(result);
        Assert.Contains(ctx.Recomendaciones, r => r.TipoRecomendacion == "desplegar");
    }

    [Fact(DisplayName = "RF13: Generate sin acceso al proyecto devuelve 403 y no genera nada")]
    public async Task Generate_SinAcceso_ForbidNoGenera()
    {
        var ctx = TestHelpers.NewContext();
        var (version, _) = await SeedConReglaAsync(ctx);
        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ejecutar_evaluacion" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.Generate(version.Id);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(ctx.Recomendaciones);
    }

    [Fact(DisplayName = "RF13: GetByResultado sin acceso al proyecto devuelve 403")]
    public async Task GetByResultado_SinAcceso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var (_, resultadoId) = await SeedConReglaAsync(ctx);
        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_evaluacion" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.GetByResultado(resultadoId);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact(DisplayName = "GetReglasByResultado devuelve el veredicto real aplicado por el motor")]
    public async Task GetReglasByResultado_DevuelveVeredicto()
    {
        var ctx = TestHelpers.NewContext();
        var (version, resultadoId) = await SeedConReglaAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_evaluacion" });
        var controller = NewController(ctx, admin);
        await controller.Generate(version.Id);

        var result = await controller.GetReglasByResultado(resultadoId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var lista = Assert.IsAssignableFrom<List<EvaluacionReglaDto>>(ok.Value);
        Assert.NotEmpty(lista);
    }

    [Fact(DisplayName = "GetByVersion devuelve las recomendaciones generadas para la version")]
    public async Task GetByVersion_DevuelveRecomendaciones()
    {
        var ctx = TestHelpers.NewContext();
        var (version, _) = await SeedConReglaAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ejecutar_evaluacion" });
        var controller = NewController(ctx, admin);
        await controller.Generate(version.Id);

        var result = await controller.GetByVersion(version.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var lista = Assert.IsAssignableFrom<List<RecomendacionDto>>(ok.Value);
        Assert.NotEmpty(lista);
    }

    [Fact(DisplayName = "RF10: GetByVersion devuelve solo la recomendación del resultado más reciente")]
    public async Task GetByVersion_VariosResultados_DevuelveSoloElMasReciente()
    {
        var ctx = TestHelpers.NewContext();
        var (version, resultadoAnterior) = await SeedConReglaAsync(ctx);
        // La ejecución anterior no alcanza el umbral; la más reciente sí.
        var metricaAnterior = ctx.Metricas.Single(m => m.ResultadoId == resultadoAnterior);
        metricaAnterior.ValorMetrica = 70m;
        var anterior = ctx.ResultadosPrueba.Single(r => r.Id == resultadoAnterior);
        anterior.FechaCarga = DateTime.UtcNow.AddDays(-1);
        var reciente = new ResultadoPrueba { VersionId = version.Id, NombreArchivo = "b.json", FechaCarga = DateTime.UtcNow };
        ctx.ResultadosPrueba.Add(reciente);
        await ctx.SaveChangesAsync();
        ctx.Metricas.Add(new Metrica { ResultadoId = reciente.Id, NombreMetrica = "tasa_exito", ValorMetrica = 95m });
        await ctx.SaveChangesAsync();

        var engine = new RecommendationEngine(ctx);
        await engine.GenerateRecommendationForResultadoAsync(resultadoAnterior);
        await engine.GenerateRecommendationForResultadoAsync(reciente.Id);

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_evaluacion" });
        var result = await NewController(ctx, admin).GetByVersion(version.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var lista = Assert.IsAssignableFrom<List<RecomendacionDto>>(ok.Value);
        var unica = Assert.Single(lista);
        Assert.Equal("desplegar", unica.TipoRecomendacion);
    }

    [Fact(DisplayName = "RF13: GetByVersion sin acceso al proyecto devuelve 403")]
    public async Task GetByVersion_SinAcceso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var (version, _) = await SeedConReglaAsync(ctx);
        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_evaluacion" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.GetByVersion(version.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }
}
