using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>Detalle por prueba del output.json (pruebas fallidas con su motivo, más lentas, desglose por módulo).</summary>
public class DetallePruebasTests
{
    // Dos módulos: Login (1 falla con mensaje propio) y Pagos (2 fallas: una con mensaje propio y otra solo en el paso que falló) y 1 omitida.
    private const string Reporte = """
    {"generator":"Robot 7.4 (Python 3.12.0 on win32)","generated":"2026-10-16T10:30:00","suite":{"name":"Roshka Smoke","status":"FAIL","elapsed_time":15,"suites":[
      {"name":"01-Login","tests":[
        {"name":"L1 Login exitoso","status":"PASS","elapsed_time":1.1},
        {"name":"L2 Login bloqueado","status":"FAIL","elapsed_time":2.3,"message":"Element 'id=error' not found"}]},
      {"name":"02-Pagos","tests":[
        {"name":"P1 Pago con tarjeta","status":"FAIL","elapsed_time":4.8,"body":[{"name":"Click Button","status":"FAIL","body":[
          {"type":"MESSAGE","level":"INFO","message":"Clicking button"},{"type":"MESSAGE","level":"FAIL","message":"Button   not\nfound"}]}]},
        {"name":"P2 Reembolso","status":"SKIP","elapsed_time":0},
        {"name":"P3 Pago con PayPal","status":"FAIL","elapsed_time":3.0,"message":"Timeout"}]}]},
     "statistics":{"total":{"pass":1,"fail":3,"skip":1}},"errors":[{"message":"x"}]}
    """;

    [Fact(DisplayName = "Detalle por prueba: extrae fallidas con su motivo, más lentas y desglose por módulo")]
    public void Extraer_DetalleCompleto()
    {
        var d = RobotFrameworkDetalle.Extraer(Reporte)!;

        Assert.Equal(3, d.TotalFallidas);
        Assert.Equal(1, d.ErroresGlobales);
        Assert.Equal(new[] { "02-Pagos", "01-Login" }, d.Modulos.Select(m => m.Nombre));                 // primero el que más falla
        Assert.Equal((0, 2, 1), (d.Modulos[0].Exitosas, d.Modulos[0].Fallidas, d.Modulos[0].Omitidas));
        Assert.Equal((1, 1, 0), (d.Modulos[1].Exitosas, d.Modulos[1].Fallidas, d.Modulos[1].Omitidas));

        Assert.Equal("Element 'id=error' not found", d.Fallidas.First(f => f.Nombre.StartsWith("L2")).Mensaje);
        Assert.Equal("Button not found", d.Fallidas.First(f => f.Nombre.StartsWith("P1")).Mensaje);     // sale del paso que falló
        Assert.Equal("02-Pagos", d.Fallidas.First(f => f.Nombre.StartsWith("P3")).Modulo);

        Assert.Equal(new[] { "P1 Pago con tarjeta", "P3 Pago con PayPal", "L2 Login bloqueado", "L1 Login exitoso" }, d.MasLentas.Select(l => l.Nombre));

        // se puede guardar y volver a leer
        var ida = RobotFrameworkDetalle.Deserializar(RobotFrameworkDetalle.Serializar(d))!;
        Assert.Equal(d.TotalFallidas, ida.TotalFallidas);
        Assert.Equal(d.Fallidas.Count, ida.Fallidas.Count);
    }

    [Fact(DisplayName = "Detalle por prueba: sin pruebas a nivel de test o con contenido inválido no hay detalle y no falla")]
    public void Extraer_SinDetalle_DevuelveNull()
    {
        const string soloResumen = """{"generator":"Robot 7.4","suite":{"name":"x","status":"PASS","elapsed_time":1},"statistics":{"total":{"pass":1,"fail":0,"skip":0}}}""";
        Assert.Null(RobotFrameworkDetalle.Extraer(soloResumen));
        Assert.Null(RobotFrameworkDetalle.Extraer("esto no es json"));
        Assert.Null(RobotFrameworkDetalle.Extraer("[1,2,3]"));
        Assert.Null(RobotFrameworkDetalle.Extraer(null));
        Assert.Null(RobotFrameworkDetalle.Extraer("   "));
        Assert.Null(RobotFrameworkDetalle.Deserializar("{malo"));

        // más de 20 fallidas: se guardan 20 y se informa el total real
        var muchas = string.Join(",", Enumerable.Range(1, 25).Select(i => $$"""{"name":"T{{i}}","status":"FAIL","elapsed_time":{{i}},"message":"m"}"""));
        var d = RobotFrameworkDetalle.Extraer("{\"suite\":{\"name\":\"S\",\"tests\":[" + muchas + "]}}")!;
        Assert.Equal(25, d.TotalFallidas);
        Assert.Equal(RobotFrameworkDetalle.MaxFallidas, d.Fallidas.Count);
        Assert.Equal(RobotFrameworkDetalle.MaxLentas, d.MasLentas.Count);
    }

    [Fact(DisplayName = "Detalle por prueba: la carga guarda el detalle y el endpoint lo devuelve (204 si no hay)")]
    public async Task Carga_GuardaDetalle_YEndpointLoDevuelve()
    {
        var ctx = TestHelpers.NewContext();
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_resultados" });
        var ingesta = new IngestaResultadosService(
            ctx, new MetricsCalculationService(ctx), new RecommendationEngine(ctx),
            TestHelpers.NewAuditoriaService(ctx, admin), new FakeSuscripcionService(), new FakeEmailService());
        var controller = new ResultadosPruebaController(ctx, TestHelpers.NewAuditoriaService(ctx, admin), ingesta, new ProyectoAccesoService(ctx));
        TestHelpers.SetUser(controller, admin);

        var p = new Proyecto { Nombre = "Detalle" };
        ctx.Proyectos.Add(p);
        await ctx.SaveChangesAsync();
        var v = new Models.Version { ProyectoId = p.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(v);
        await ctx.SaveChangesAsync();

        CreateResultadoPruebaDto Dto(string? contenido) => new()
        {
            VersionId = v.Id, NombreArchivo = "output.json", TotalPruebas = 5, PruebasExitosas = 1, PruebasFallidas = 3, PruebasOmitidas = 1,
            Cobertura = 80, TiempoEjecucion = 15, ContenidoReporte = contenido,
        };

        var conDetalle = (ResultadoPruebaDto)(await ingesta.IngestarAsync(Dto(Reporte), 1)).Body;
        var sinDetalle = (ResultadoPruebaDto)(await ingesta.IngestarAsync(Dto(null), 1)).Body;
        Assert.Single(ctx.ResultadosDetalle);                          // solo el que trajo el archivo completo

        var ok = Assert.IsType<OkObjectResult>(await controller.GetDetalle(conDetalle.Id));
        var detalle = Assert.IsType<DetalleRobotFramework>(ok.Value);
        Assert.Equal(3, detalle.TotalFallidas);
        Assert.IsType<NoContentResult>(await controller.GetDetalle(sinDetalle.Id));

        // sin acceso al proyecto: 403 (RF13)
        var ajeno = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_resultados" }, new[] { 999 });
        var controllerAjeno = new ResultadosPruebaController(ctx, TestHelpers.NewAuditoriaService(ctx, ajeno), ingesta, new ProyectoAccesoService(ctx));
        TestHelpers.SetUser(controllerAjeno, ajeno);
        Assert.IsType<ForbidResult>(await controllerAjeno.GetDetalle(conDetalle.Id));
    }

    [Fact(DisplayName = "Detalle por prueba: el reporte ejecutivo nombra las pruebas y módulos con fallas")]
    public async Task Reporte_IncluyeDetalleDeFallas()
    {
        var ctx = TestHelpers.NewContext();
        var (versionId, _, resultadoId) = await ReporteEjecutivoTests.SeedAsync(ctx);
        ctx.ResultadosDetalle.Add(new ResultadoDetalle { ResultadoId = resultadoId, DetalleJson = RobotFrameworkDetalle.Serializar(RobotFrameworkDetalle.Extraer(Reporte)!) });
        await ctx.SaveChangesAsync();
        var servicio = new ReporteEjecutivoService(ctx);

        var conDetalle = (await servicio.ObtenerDatosAsync(versionId, null))!;
        Assert.Equal(3, conDetalle.Detalle!.TotalFallidas);
        Assert.Contains(ReporteEjecutivoPdf.Acciones(conDetalle), a => a.Contains("«02-Pagos»") && a.Contains("2 de las 3"));

        var sinDetalle = conDetalle with { Detalle = null };
        foreach (var perfil in new[] { PerfilReporte.Tecnico, PerfilReporte.Qa })
            Assert.True(servicio.GenerarPdf(conDetalle, perfil, "Tester").Length > servicio.GenerarPdf(sinDetalle, perfil, "Tester").Length);
        Assert.True(servicio.GenerarPdf(conDetalle, PerfilReporte.Gerencia, "Tester").Length > 2000);
        Assert.DoesNotContain(ReporteEjecutivoPdf.Acciones(sinDetalle), a => a.Contains("Empezar por el módulo"));
    }
}
