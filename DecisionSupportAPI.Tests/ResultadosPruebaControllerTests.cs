using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de ResultadosPruebaController (RF03, RF13).</summary>
public class ResultadosPruebaControllerTests
{
    private static ResultadosPruebaController NewController(ApplicationDbContext ctx, ClaimsPrincipal user)
    {
        var ingesta = new IngestaResultadosService(
            ctx, new MetricsCalculationService(ctx), new RecommendationEngine(ctx),
            TestHelpers.NewAuditoriaService(ctx, user), new FakeSuscripcionService(), new FakeEmailService());
        var controller = new ResultadosPruebaController(ctx, TestHelpers.NewAuditoriaService(ctx, user), ingesta, new ProyectoAccesoService(ctx));
        TestHelpers.SetUser(controller, user);
        return controller;
    }

    private static async Task<Models.Version> SeedVersionAsync(ApplicationDbContext ctx)
    {
        var p = new Proyecto { Nombre = "Proyecto R", Estado = "activo" };
        ctx.Proyectos.Add(p);
        await ctx.SaveChangesAsync();
        var v = new Models.Version { ProyectoId = p.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(v);
        await ctx.SaveChangesAsync();
        return v;
    }

    [Fact(DisplayName = "GetByVersion devuelve los resultados de la versión")]
    public async Task GetByVersion_DevuelveResultados()
    {
        var ctx = TestHelpers.NewContext();
        var version = await SeedVersionAsync(ctx);
        ctx.ResultadosPrueba.Add(new ResultadoPrueba { VersionId = version.Id, NombreArchivo = "a.json" });
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_resultados" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetByVersion(version.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(Assert.IsAssignableFrom<List<ResultadoPruebaDto>>(ok.Value));
    }

    [Fact(DisplayName = "RF13: GetByVersion sin acceso al proyecto devuelve 403")]
    public async Task GetByVersion_SinAcceso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var version = await SeedVersionAsync(ctx);

        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_resultados" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.GetByVersion(version.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact(DisplayName = "Create sin acceso al proyecto de la versión devuelve 403 y no crea el resultado")]
    public async Task Create_SinAcceso_ForbidNoCrea()
    {
        var ctx = TestHelpers.NewContext();
        var version = await SeedVersionAsync(ctx);

        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "cargar_resultados" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.Create(new CreateResultadoPruebaDto
        {
            VersionId = version.Id, NombreArchivo = "a.json",
            TotalPruebas = 10, PruebasExitosas = 10, PruebasFallidas = 0, Cobertura = 100m, TiempoEjecucion = 5m,
        });

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Empty(ctx.ResultadosPrueba);
    }

    [Fact(DisplayName = "Create con acceso al proyecto crea el resultado correctamente")]
    public async Task Create_ConAcceso_Crea()
    {
        var ctx = TestHelpers.NewContext();
        var version = await SeedVersionAsync(ctx);

        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "cargar_resultados" }, new[] { version.ProyectoId });
        var controller = NewController(ctx, analista);

        var result = await controller.Create(new CreateResultadoPruebaDto
        {
            VersionId = version.Id, NombreArchivo = "a.json",
            TotalPruebas = 10, PruebasExitosas = 10, PruebasFallidas = 0, Cobertura = 100m, TiempoEjecucion = 5m,
        });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Single(ctx.ResultadosPrueba);
    }

    [Fact(DisplayName = "Validar con estado inválido devuelve 400")]
    public async Task Validar_EstadoInvalido_BadRequest()
    {
        var ctx = TestHelpers.NewContext();
        var version = await SeedVersionAsync(ctx);
        var resultado = new ResultadoPrueba { VersionId = version.Id, NombreArchivo = "a.json" };
        ctx.ResultadosPrueba.Add(resultado);
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "cargar_resultados" });
        var controller = NewController(ctx, admin);

        var result = await controller.Validar(resultado.Id, new ValidarResultadoDto { EstadoValidacion = "no_valido" });

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
