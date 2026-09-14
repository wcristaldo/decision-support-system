using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de MetricasController (RF06, RF13).</summary>
public class MetricasControllerTests
{
    private static MetricasController NewController(ApplicationDbContext ctx, System.Security.Claims.ClaimsPrincipal user)
    {
        var controller = new MetricasController(new MetricsCalculationService(ctx), new ProyectoAccesoService(ctx));
        TestHelpers.SetUser(controller, user);
        return controller;
    }

    private static async Task<(Models.Version version, int resultadoId)> SeedAsync(ApplicationDbContext ctx)
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
        await new MetricsCalculationService(ctx).CalculateMetricsAsync(r.Id, 10, 9, 1, 90m, 5m);
        return (v, r.Id);
    }

    [Fact(DisplayName = "GetByVersion devuelve las métricas del último resultado")]
    public async Task GetByVersion_DevuelveMetricas()
    {
        var ctx = TestHelpers.NewContext();
        var (version, _) = await SeedAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_resultados" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetByVersion(version.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotEmpty(Assert.IsAssignableFrom<List<MetricaDto>>(ok.Value));
    }

    [Fact(DisplayName = "RF13: GetByVersion sin acceso al proyecto devuelve 403")]
    public async Task GetByVersion_SinAcceso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var (version, _) = await SeedAsync(ctx);
        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_resultados" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.GetByVersion(version.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact(DisplayName = "RF13: GetByResultado sin acceso al proyecto devuelve 403")]
    public async Task GetByResultado_SinAcceso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var (_, resultadoId) = await SeedAsync(ctx);
        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_resultados" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.GetByResultado(resultadoId);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact(DisplayName = "GetByResultado con acceso devuelve las métricas de ese resultado")]
    public async Task GetByResultado_ConAcceso_DevuelveMetricas()
    {
        var ctx = TestHelpers.NewContext();
        var (version, resultadoId) = await SeedAsync(ctx);
        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_resultados" }, new[] { version.ProyectoId });
        var controller = NewController(ctx, analista);

        var result = await controller.GetByResultado(resultadoId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotEmpty(Assert.IsAssignableFrom<List<MetricaDto>>(ok.Value));
    }
}
