using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de ReportsController (CU-05 — ingesta automatizada CI/CD del output.json).</summary>
public class ReportsControllerTests
{
    internal const string OutputValido = """
        {
          "generator": "Robot 7.4 (Python 3.12.0 on win32)",
          "generated": "2026-09-27T10:00:00.000000",
          "suite": { "name": "01-Login", "status": "PASS", "elapsed_time": 12.43 },
          "statistics": { "total": { "label": "All Tests", "pass": 3, "fail": 0, "skip": 0 } },
          "errors": []
        }
        """;

    private static (ApplicationDbContext ctx, ReportsController controller) Arrange(bool planConCicd = true)
    {
        var ctx = TestHelpers.NewContext();
        var ingesta = new IngestaResultadosService(
            ctx, new MetricsCalculationService(ctx), new RecommendationEngine(ctx),
            TestHelpers.NewAuditoriaService(ctx), new FakeSuscripcionService(), new FakeEmailService());
        var controller = new ReportsController(ingesta, ctx, new FakeSuscripcionService(planConCicd));
        TestHelpers.SetUser(controller, TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "cargar_resultados" }));
        return (ctx, controller);
    }

    private static async Task<Models.Version> SeedVersionAsync(ApplicationDbContext ctx)
    {
        var p = new Proyecto { Nombre = "Login", Estado = "activo" };
        ctx.Proyectos.Add(p);
        await ctx.SaveChangesAsync();
        var v = new Models.Version { ProyectoId = p.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(v);
        ctx.ReglasEvaluacion.Add(new ReglaEvaluacion { Nombre = "Tasa éxito", Criterio = "tasa_exito", Umbral = 90m, Estado = "activo" });
        await ctx.SaveChangesAsync();
        return v;
    }

    private static IFormFile Archivo(string contenido, string nombre = "output.json")
    {
        var bytes = Encoding.UTF8.GetBytes(contenido);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "archivo", nombre);
    }

    private static JsonElement Cuerpo(IActionResult result) =>
        JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsAssignableFrom<ObjectResult>(result).Value)).RootElement;

    [Fact(DisplayName = "CP-09: Ingest con un output.json válido registra el resultado, extrae las métricas y devuelve 201 con la recomendación")]
    public async Task Ingest_ArchivoValido_Devuelve201ConMetricasYRecomendacion()
    {
        var (ctx, controller) = Arrange();
        var v = await SeedVersionAsync(ctx);

        var result = await controller.Ingest(new IngestaReporteRequest { VersionId = v.Id, Archivo = Archivo(OutputValido), Observaciones = "pipeline" });

        Assert.Equal(201, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        var cuerpo = Cuerpo(result);
        Assert.Equal(3, cuerpo.GetProperty("reporte").GetProperty("totalPruebas").GetInt32());
        Assert.Equal("01-Login", cuerpo.GetProperty("reporte").GetProperty("suite").GetString());
        Assert.Equal("desplegar", cuerpo.GetProperty("recomendacion").GetString());
        var guardado = Assert.Single(ctx.ResultadosPrueba);
        Assert.Equal("output.json", guardado.NombreArchivo);
        Assert.Contains(ctx.Metricas, m => m.ResultadoId == guardado.Id && m.NombreMetrica == "tasa_exito" && m.ValorMetrica == 100m);
    }

    [Fact(DisplayName = "Ingest sin archivo devuelve 400")]
    public async Task Ingest_SinArchivo_Devuelve400()
    {
        var (ctx, controller) = Arrange();
        var v = await SeedVersionAsync(ctx);

        var result = await controller.Ingest(new IngestaReporteRequest { VersionId = v.Id });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(ctx.ResultadosPrueba);
    }

    [Fact(DisplayName = "Ingest con un archivo que no es .json devuelve 400")]
    public async Task Ingest_ExtensionInvalida_Devuelve400()
    {
        var (ctx, controller) = Arrange();
        var v = await SeedVersionAsync(ctx);

        var result = await controller.Ingest(new IngestaReporteRequest { VersionId = v.Id, Archivo = Archivo(OutputValido, "output.xml") });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "Ingest con un JSON que no es de Robot Framework devuelve 422 y no registra nada")]
    public async Task Ingest_FormatoInvalido_Devuelve422()
    {
        var (ctx, controller) = Arrange();
        var v = await SeedVersionAsync(ctx);

        var result = await controller.Ingest(new IngestaReporteRequest { VersionId = v.Id, Archivo = Archivo("""{ "generator": "JUnit" }""") });

        Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Empty(ctx.ResultadosPrueba);
    }

    [Fact(DisplayName = "Ingest con versión inexistente devuelve 404")]
    public async Task Ingest_VersionInexistente_Devuelve404()
    {
        var (_, controller) = Arrange();

        var result = await controller.Ingest(new IngestaReporteRequest { VersionId = 999999, Archivo = Archivo(OutputValido) });

        Assert.Equal(404, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact(DisplayName = "Ingest rechaza con 402 la carga automática si el plan activo no incluye la integración CI/CD")]
    public async Task Ingest_PlanSinIntegracionCicd_Devuelve402()
    {
        var (ctx, controller) = Arrange(planConCicd: false);
        var v = await SeedVersionAsync(ctx);

        var result = await controller.Ingest(new IngestaReporteRequest { VersionId = v.Id, Archivo = Archivo(OutputValido) });

        Assert.Equal(402, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Equal("FEATURE_NO_DISPONIBLE", Cuerpo(result).GetProperty("codigo").GetString());
        Assert.Empty(ctx.ResultadosPrueba);
    }
}

/// <summary>Validaciones del output.json de Robot Framework en el servidor (mismas reglas que el frontend).</summary>
public class RobotFrameworkParserTests
{
    private static string Json(string status = "PASS", string elapsed = "12.5", string pass = "3", string fail = "0", string skip = "0",
                               string generator = "\"Robot 7.4 (Python 3.12.0 on win32)\"", bool conGenerated = true, bool conSuite = true, bool conStatistics = true) =>
        "{" + $"\"generator\": {generator}"
            + (conGenerated ? ", \"generated\": \"2026-09-27T10:00:00\"" : "")
            + (conSuite ? $", \"suite\": {{ \"name\": \"Suite\", \"status\": \"{status}\", \"elapsed_time\": {elapsed} }}" : "")
            + (conStatistics ? $", \"statistics\": {{ \"total\": {{ \"pass\": {pass}, \"fail\": {fail}, \"skip\": {skip} }} }}" : "")
            + "}";

    [Fact(DisplayName = "Parsear un output.json válido extrae contadores, tiempo, cobertura y herramienta")]
    public void Parsear_Valido()
    {
        var m = RobotFrameworkParser.Parsear(Json(status: "FAIL", pass: "92", fail: "3", skip: "5", elapsed: "45"));

        Assert.Equal((100, 92, 3, 5), (m.TotalPruebas, m.PruebasExitosas, m.PruebasFallidas, m.PruebasOmitidas));
        Assert.Equal(45m, m.TiempoEjecucion);
        Assert.Equal(95m, m.Cobertura);
        Assert.Equal("Robot 7.4", m.Herramienta);
        Assert.Equal("FAIL", m.EstadoSuite);
    }

    [Theory(DisplayName = "Parsear rechaza archivos que no son un output.json válido de Robot Framework")]
    [InlineData("no es json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"generator\": \"JUnit 5\", \"generated\": \"x\"}")]
    public void Parsear_Invalido_Lanza(string contenido) =>
        Assert.Throws<FormatoReporteInvalidoException>(() => RobotFrameworkParser.Parsear(contenido));

    [Fact(DisplayName = "Parsear exige el campo generated")]
    public void Parsear_SinGenerated() =>
        Assert.Throws<FormatoReporteInvalidoException>(() => RobotFrameworkParser.Parsear(Json(conGenerated: false)));

    [Fact(DisplayName = "Parsear exige el objeto suite")]
    public void Parsear_SinSuite() =>
        Assert.Throws<FormatoReporteInvalidoException>(() => RobotFrameworkParser.Parsear(Json(conSuite: false)));

    [Fact(DisplayName = "Parsear exige statistics.total")]
    public void Parsear_SinStatistics() =>
        Assert.Throws<FormatoReporteInvalidoException>(() => RobotFrameworkParser.Parsear(Json(conStatistics: false)));

    [Theory(DisplayName = "Parsear valida estado, tiempo y contadores")]
    [InlineData("SKIP", "1", "3", "0", "0")]
    [InlineData("PASS", "-1", "3", "0", "0")]
    [InlineData("PASS", "1", "-3", "0", "0")]
    [InlineData("PASS", "1", "2.5", "0", "0")]
    [InlineData("PASS", "1", "0", "0", "0")]
    [InlineData("FAIL", "1", "3", "0", "0")]
    [InlineData("PASS", "1", "3", "1", "0")]
    public void Parsear_DatosIncoherentes_Lanza(string status, string elapsed, string pass, string fail, string skip) =>
        Assert.Throws<FormatoReporteInvalidoException>(() => RobotFrameworkParser.Parsear(Json(status, elapsed, pass, fail, skip)));
}
