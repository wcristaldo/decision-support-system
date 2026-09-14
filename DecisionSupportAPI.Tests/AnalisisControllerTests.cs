using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de AnalisisController (RF10, RF11, RF13).</summary>
public class AnalisisControllerTests
{
    private static AnalisisController NewController(ApplicationDbContext ctx, System.Security.Claims.ClaimsPrincipal user)
    {
        var controller = new AnalisisController(ctx, new FakeSuscripcionService(), new ReporteExportService(), new ProyectoAccesoService(ctx));
        TestHelpers.SetUser(controller, user);
        return controller;
    }

    private static async Task<Proyecto> SeedHistorialAsync(ApplicationDbContext ctx)
    {
        var p = new Proyecto { Nombre = "Proyecto Historial", Estado = "activo" };
        ctx.Proyectos.Add(p);
        await ctx.SaveChangesAsync();
        var v = new Models.Version { ProyectoId = p.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(v);
        await ctx.SaveChangesAsync();
        var r = new ResultadoPrueba { VersionId = v.Id, NombreArchivo = "a.json" };
        ctx.ResultadosPrueba.Add(r);
        await ctx.SaveChangesAsync();
        var eval = new Evaluacion { ResultadoId = r.Id, FechaEvaluacion = DateTime.UtcNow };
        ctx.Evaluaciones.Add(eval);
        await ctx.SaveChangesAsync();
        ctx.Recomendaciones.Add(new Recomendacion { EvaluacionId = eval.Id, TipoRecomendacion = "desplegar", FechaGeneracion = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
        return p;
    }

    [Fact(DisplayName = "GetHistorial como Administrador devuelve todas las entradas")]
    public async Task GetHistorial_Administrador_DevuelveTodo()
    {
        var ctx = TestHelpers.NewContext();
        await SeedHistorialAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_evaluacion" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetHistorial();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(Assert.IsAssignableFrom<List<AnalisisHistorialDto>>(ok.Value));
    }

    [Fact(DisplayName = "RF13: GetHistorial para un usuario sin proyectos asignados devuelve lista vacía")]
    public async Task GetHistorial_SinProyectosAsignados_ListaVacia()
    {
        var ctx = TestHelpers.NewContext();
        await SeedHistorialAsync(ctx);
        var gerente = TestHelpers.BuildUser(2, new[] { "Gerente QA" }, new[] { "ver_evaluacion" }, Array.Empty<int>());
        var controller = NewController(ctx, gerente);

        var result = await controller.GetHistorial();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(Assert.IsAssignableFrom<List<AnalisisHistorialDto>>(ok.Value));
    }

    [Fact(DisplayName = "RF13: GetHistorial para un usuario con el proyecto asignado sí lo incluye")]
    public async Task GetHistorial_ConProyectoAsignado_LoIncluye()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedHistorialAsync(ctx);
        var gerente = TestHelpers.BuildUser(2, new[] { "Gerente QA" }, new[] { "ver_evaluacion" }, new[] { proyecto.Id });
        var controller = NewController(ctx, gerente);

        var result = await controller.GetHistorial();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(Assert.IsAssignableFrom<List<AnalisisHistorialDto>>(ok.Value));
    }

    [Fact(DisplayName = "ExportarPdf devuelve un archivo PDF")]
    public async Task ExportarPdf_DevuelveArchivoPdf()
    {
        var ctx = TestHelpers.NewContext();
        await SeedHistorialAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_evaluacion" });
        var controller = NewController(ctx, admin);

        var result = await controller.ExportarPdf();

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
    }
}
