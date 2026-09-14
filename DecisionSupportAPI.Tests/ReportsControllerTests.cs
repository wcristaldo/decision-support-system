using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de ReportsController (CU-05 — ingesta automatizada CI/CD).</summary>
public class ReportsControllerTests
{
    private static (ApplicationDbContext ctx, ReportsController controller) Arrange()
    {
        var ctx = TestHelpers.NewContext();
        var ingesta = new IngestaResultadosService(
            ctx, new MetricsCalculationService(ctx), new RecommendationEngine(ctx),
            TestHelpers.NewAuditoriaService(ctx), new FakeSuscripcionService(), new FakeEmailService());
        var controller = new ReportsController(ingesta);
        TestHelpers.SetUser(controller, TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "cargar_resultados" }));
        return (ctx, controller);
    }

    [Fact(DisplayName = "Ingest con datos válidos delega al pipeline de ingesta y devuelve 201")]
    public async Task Ingest_DatosValidos_Devuelve201()
    {
        var (ctx, controller) = Arrange();
        var p = new Proyecto { Nombre = "P", Estado = "activo" };
        ctx.Proyectos.Add(p);
        await ctx.SaveChangesAsync();
        var v = new Models.Version { ProyectoId = p.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(v);
        await ctx.SaveChangesAsync();

        var result = await controller.Ingest(new CreateResultadoPruebaDto
        {
            VersionId = v.Id, NombreArchivo = "ci.json",
            TotalPruebas = 20, PruebasExitosas = 20, PruebasFallidas = 0, Cobertura = 100m, TiempoEjecucion = 8m,
        });

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objResult.StatusCode);
    }

    [Fact(DisplayName = "Ingest con versión inexistente devuelve 404")]
    public async Task Ingest_VersionInexistente_Devuelve404()
    {
        var (_, controller) = Arrange();

        var result = await controller.Ingest(new CreateResultadoPruebaDto
        {
            VersionId = 999999, NombreArchivo = "ci.json",
            TotalPruebas = 10, PruebasExitosas = 10, PruebasFallidas = 0, Cobertura = 100m, TiempoEjecucion = 1m,
        });

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objResult.StatusCode);
    }
}
