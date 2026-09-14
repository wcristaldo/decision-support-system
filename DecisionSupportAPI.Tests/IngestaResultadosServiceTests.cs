using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de IngestaResultadosService — pipeline único de
/// ingesta (RF03-RF09) usado tanto por la carga manual como por CU-05
/// (ingesta automatizada CI/CD, ReportsController).</summary>
public class IngestaResultadosServiceTests
{
    private static (ApplicationDbContext ctx, IngestaResultadosService service, int versionId) Arrange()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = new Proyecto { Nombre = "Proyecto Ingesta", Estado = "activo" };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        var version = new Models.Version { ProyectoId = proyecto.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(version);
        ctx.SaveChanges();

        // Regla global mínima para que RecommendationEngine tenga un criterio
        // real contra el cual evaluar (sin ninguna regla, no genera recomendación).
        ctx.ReglasEvaluacion.Add(new ReglaEvaluacion { Nombre = "Tasa de éxito mínima", Criterio = "tasa_exito", Umbral = 90m, Estado = "activo" });
        ctx.SaveChanges();

        var metrics = new MetricsCalculationService(ctx);
        var recommendationEngine = new RecommendationEngine(ctx);
        var httpContext = new DefaultHttpContext();
        httpContext.User = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "cargar_resultados" });
        var auditoria = new AuditoriaService(ctx, new HttpContextAccessor { HttpContext = httpContext }, NullLogger<AuditoriaService>.Instance);

        var service = new IngestaResultadosService(
            ctx, metrics, recommendationEngine, auditoria,
            new FakeSuscripcionService(), new FakeEmailService());

        return (ctx, service, version.Id);
    }

    private static CreateResultadoPruebaDto DtoValido(int versionId) => new()
    {
        VersionId = versionId,
        NombreArchivo = "output.json",
        TotalPruebas = 50,
        PruebasExitosas = 48,
        PruebasFallidas = 2,
        PruebasOmitidas = 0,
        Cobertura = 90m,
        TiempoEjecucion = 30m,
    };

    [Fact(DisplayName = "IngestarAsync con datos válidos persiste resultado, métricas y recomendación")]
    public async Task IngestarAsync_DatosValidos_Persiste201()
    {
        var (ctx, service, versionId) = Arrange();

        var resultado = await service.IngestarAsync(DtoValido(versionId), usuarioCargaId: 1);

        Assert.Equal(201, resultado.StatusCode);
        Assert.Single(ctx.ResultadosPrueba);
        Assert.NotEmpty(ctx.Metricas);
        Assert.NotEmpty(ctx.Recomendaciones);
        Assert.Single(ctx.Auditoria.Where(a => a.Accion == "Create"));
    }

    [Fact(DisplayName = "IngestarAsync rechaza cuando exitosas+fallidas+omitidas no suma el total declarado")]
    public async Task IngestarAsync_TotalInconsistente_Rechaza400()
    {
        var (_, service, versionId) = Arrange();
        var dto = DtoValido(versionId);
        dto.TotalPruebas = 100; // no coincide con 48+2+0

        var resultado = await service.IngestarAsync(dto);

        Assert.Equal(400, resultado.StatusCode);
    }

    [Fact(DisplayName = "IngestarAsync recalcula la cobertura en el servidor en vez de confiar en el valor del cliente")]
    public async Task IngestarAsync_RecalculaCoberturaEnServidor()
    {
        var (ctx, service, versionId) = Arrange();
        var dto = DtoValido(versionId);
        dto.Cobertura = 1m; // valor "falso" enviado por el cliente, debe ser ignorado

        var resultado = await service.IngestarAsync(dto);
        Assert.Equal(201, resultado.StatusCode);

        var resultadoId = ((ResultadoPruebaDto)resultado.Body).Id;
        var coberturaPersistida = ctx.Metricas
            .First(m => m.ResultadoId == resultadoId && m.NombreMetrica == "cobertura")
            .ValorMetrica;

        // (48 exitosas + 2 fallidas) / 50 total * 100 = 100%, no el 1% enviado.
        Assert.Equal(100m, coberturaPersistida);
    }

    [Fact(DisplayName = "IngestarAsync con versión inexistente devuelve 404")]
    public async Task IngestarAsync_VersionInexistente_Devuelve404()
    {
        var (_, service, _) = Arrange();
        var dto = DtoValido(versionId: 999999);

        var resultado = await service.IngestarAsync(dto);

        Assert.Equal(404, resultado.StatusCode);
    }
}
