using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Json;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>Reporte ejecutivo por perfil (PDF) y serialización de fechas en UTC.</summary>
public class ReporteEjecutivoTests
{
    /// <summary>Proyecto con dos versiones evaluadas: una anterior que desplegaba bien y la actual (CP-02: 60 pasaron, 5
    /// fallaron, 35 omitidas) que no cumple la cobertura. Devuelve el id de la versión actual.</summary>
    internal static async Task<(int VersionId, int ProyectoId, int ResultadoId)> SeedAsync(ApplicationDbContext ctx, bool conDecision = false)
    {
        var proyecto = new Proyecto { Nombre = "Login", TipoSolucion = "web" };
        ctx.Proyectos.Add(proyecto);
        var decisor = new Usuario { Nombre = "Laura", Apellido = "Gómez", Email = "laura@roshka.com", PasswordHash = "x" };
        ctx.Usuarios.Add(decisor);
        await ctx.SaveChangesAsync();

        ctx.ReglasEvaluacion.AddRange(
            new ReglaEvaluacion { Nombre = "Tasa de éxito mínima", Criterio = "tasa_exito", Umbral = 90m },
            new ReglaEvaluacion { Nombre = "Cobertura mínima", Criterio = "cobertura", Umbral = 80m },
            new ReglaEvaluacion { Nombre = "Tiempo máximo", Criterio = "tiempo_ejecucion", Umbral = 120m });
        await ctx.SaveChangesAsync();
        var reglas = ctx.ReglasEvaluacion.OrderBy(r => r.Id).ToList();

        async Task<(int VersionId, int ResultadoId, int RecomendacionId)> Evaluacion(string numero, DateTime fecha, int pass, int fail, int skip, string tipo, string[] veredictos)
        {
            var version = new Models.Version { ProyectoId = proyecto.Id, NumeroVersion = numero };
            ctx.Versiones.Add(version);
            await ctx.SaveChangesAsync();
            var total = pass + fail + skip;
            var resultado = new ResultadoPrueba { VersionId = version.Id, NombreArchivo = $"{numero}.json", FechaCarga = fecha, UsuarioCargaId = decisor.IdUsuario };
            ctx.ResultadosPrueba.Add(resultado);
            await ctx.SaveChangesAsync();
            Metrica M(string n, decimal v, string u) => new() { ResultadoId = resultado.Id, NombreMetrica = n, ValorMetrica = v, Unidad = u };
            ctx.Metricas.AddRange(M("total_pruebas", total, "casos"), M("pruebas_exitosas", pass, "casos"), M("pruebas_fallidas", fail, "casos"),
                M("pruebas_omitidas", skip, "casos"), M("tasa_exito", (decimal)pass / total * 100, "%"), M("tasa_fallo", (decimal)fail / total * 100, "%"),
                M("cobertura", (decimal)(pass + fail) / total * 100, "%"), M("tiempo_ejecucion", 30, "segundos"));
            var ev = new Evaluacion { ResultadoId = resultado.Id, FechaEvaluacion = fecha, EstadoEvaluacion = "completada" };
            ctx.Evaluaciones.Add(ev);
            await ctx.SaveChangesAsync();
            for (var i = 0; i < reglas.Count; i++)
                ctx.EvaluacionReglas.Add(new EvaluacionRegla { EvaluacionId = ev.Id, ReglaId = reglas[i].Id, ResultadoRegla = veredictos[i], Observacion = $"{reglas[i].Criterio}: …" });
            var rec = new Recomendacion { EvaluacionId = ev.Id, TipoRecomendacion = tipo, Justificacion = "Detalle", FechaGeneracion = fecha };
            ctx.Recomendaciones.Add(rec);
            await ctx.SaveChangesAsync();
            return (version.Id, resultado.Id, rec.Id);
        }

        await Evaluacion("1.0.0", DateTime.UtcNow.AddDays(-3), 92, 3, 5, "desplegar", new[] { "cumple", "cumple", "cumple" });
        var actual = await Evaluacion("1.0.1", DateTime.UtcNow.AddDays(-1), 60, 5, 35, "no_desplegar", new[] { "no_cumple", "no_cumple", "cumple" });

        if (conDecision)
        {
            ctx.DecisionesDespliegue.Add(new DecisionDespliegue { RecomendacionId = actual.RecomendacionId, UsuarioDecisorId = decisor.IdUsuario, DecisionFinal = "rechazado", Comentario = "Cobertura insuficiente", EsOverride = false });
            await ctx.SaveChangesAsync();
        }
        return (actual.VersionId, proyecto.Id, actual.ResultadoId);
    }

    private static AnalisisController NewController(ApplicationDbContext ctx, System.Security.Claims.ClaimsPrincipal user, bool exportarPdfDisponible = true)
    {
        var controller = new AnalisisController(ctx, new FakeSuscripcionService(exportarPdfDisponible), new ReporteExportService(), new ProyectoAccesoService(ctx), new ReporteEjecutivoService(ctx));
        TestHelpers.SetUser(controller, user);
        return controller;
    }

    [Fact(DisplayName = "Reporte ejecutivo: reúne métricas, criterios, evaluación anterior y decisión de la versión")]
    public async Task ObtenerDatos_ReunePrimerosDatosDeLaVersion()
    {
        var ctx = TestHelpers.NewContext();
        var (versionId, _, resultadoId) = await SeedAsync(ctx, conDecision: true);
        var servicio = new ReporteEjecutivoService(ctx);

        var d = await servicio.ObtenerDatosAsync(versionId, null);

        Assert.NotNull(d);
        Assert.Equal(resultadoId, d!.ResultadoId);
        Assert.Equal("NO_DESPLEGAR", d.Recomendacion);
        Assert.Equal((100, 60, 5, 35), (d.Total!.Value, d.Exitosas!.Value, d.Fallidas!.Value, d.Omitidas!.Value));
        Assert.Equal(65m, d.Cobertura);
        Assert.Equal(3, d.Criterios.Count);
        Assert.Equal("no_cumple", d.Criterios.First(c => c.Criterio == "cobertura").Veredicto);
        Assert.Equal(2, d.Historial.Count);
        Assert.Equal("1.0.0", d.Anterior!.Version);
        Assert.Equal("desplegar", d.Anterior.Recomendacion.ToLowerInvariant());
        Assert.Equal("rechazado", d.Decision!.Decision);
        Assert.Equal((1, 1), (d.AdherenciaTotal, d.AdherenciaAlineadas));

        // versión inexistente, o sin resultados cargados: no hay reporte
        Assert.Null(await servicio.ObtenerDatosAsync(9999, null));
        var vacia = new Models.Version { ProyectoId = 1, NumeroVersion = "9.9.9" };
        ctx.Versiones.Add(vacia);
        await ctx.SaveChangesAsync();
        Assert.Null(await servicio.ObtenerDatosAsync(vacia.Id, null));
    }

    [Fact(DisplayName = "Reporte ejecutivo: genera un PDF distinto para cada perfil y las acciones explican qué corregir")]
    public async Task GenerarPdf_CadaPerfil_YAcciones()
    {
        var ctx = TestHelpers.NewContext();
        var (versionId, _, _) = await SeedAsync(ctx);
        var servicio = new ReporteEjecutivoService(ctx);
        var d = (await servicio.ObtenerDatosAsync(versionId, null))!;

        var tamanos = new HashSet<int>();
        foreach (var perfil in Enum.GetValues<PerfilReporte>())
        {
            var pdf = servicio.GenerarPdf(d, perfil, "Tester");
            Assert.True(pdf.Length > 2000);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
            tamanos.Add(pdf.Length);
        }
        Assert.Equal(3, tamanos.Count);                       // los tres contenidos son diferentes

        var acciones = ReporteEjecutivoPdf.Acciones(d);
        Assert.Contains(acciones, a => a.Contains("las 35 pruebas omitidas") && a.Contains("65,0 %"));
        Assert.Contains(acciones, a => a.Contains("las 5 pruebas que fallan"));
        Assert.Contains(acciones, a => a.Contains("Registrar la decisión"));          // todavía no hay decisión

        // sin evaluación se dice claramente
        var sinEval = d with { Recomendacion = "SIN_EVALUACION", Criterios = new List<ReporteCriterio>() };
        Assert.Contains("todavía no tiene una evaluación", ReporteEjecutivoPdf.FraseResultado(sinEval.Recomendacion));
        Assert.Contains(ReporteEjecutivoPdf.Acciones(sinEval), a => a.Contains("Cargar los resultados"));
        Assert.True(servicio.GenerarPdf(sinEval, PerfilReporte.Qa, "Tester").Length > 1000);
    }

    [Fact(DisplayName = "Reporte ejecutivo: el endpoint respeta el rol, el plan y la existencia de resultados")]
    public async Task Endpoint_PerfilesPermisosYErrores()
    {
        var ctx = TestHelpers.NewContext();
        var (versionId, _, _) = await SeedAsync(ctx);
        var permisos = new[] { "ver_evaluacion" };
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, permisos);
        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, permisos, new[] { 1 });
        var lider = TestHelpers.BuildUser(3, new[] { "Líder Técnico" }, permisos, new[] { 1 });

        // el administrador recibe el PDF (perfil por defecto: gerencia)
        var ok = Assert.IsType<FileContentResult>(await NewController(ctx, admin).ReporteEjecutivo(versionId));
        Assert.Equal("application/pdf", ok.ContentType);
        Assert.Contains("Gerencia", ok.FileDownloadName);

        // cada rol recibe su perfil por defecto
        Assert.Contains("Tecnico", Assert.IsType<FileContentResult>(await NewController(ctx, lider).ReporteEjecutivo(versionId)).FileDownloadName);
        Assert.Contains("Qa", Assert.IsType<FileContentResult>(await NewController(ctx, analista).ReporteEjecutivo(versionId)).FileDownloadName);

        // un analista no puede pedir el reporte de gerencia ni el técnico
        Assert.Equal(403, Assert.IsType<ObjectResult>(await NewController(ctx, analista).ReporteEjecutivo(versionId, "gerencia")).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await NewController(ctx, analista).ReporteEjecutivo(versionId, "tecnico")).StatusCode);

        // perfil inválido, versión sin resultados y plan sin exportación de PDF
        Assert.IsType<BadRequestObjectResult>(await NewController(ctx, admin).ReporteEjecutivo(versionId, "otro"));
        Assert.IsType<NotFoundObjectResult>(await NewController(ctx, admin).ReporteEjecutivo(9999));
        Assert.Equal(402, Assert.IsType<ObjectResult>(await NewController(ctx, admin, exportarPdfDisponible: false).ReporteEjecutivo(versionId)).StatusCode);

        // un usuario sin el proyecto asignado no accede (RF13)
        var ajeno = TestHelpers.BuildUser(4, new[] { "Analista QA" }, permisos, new[] { 999 });
        Assert.IsType<ForbidResult>(await NewController(ctx, ajeno).ReporteEjecutivo(versionId));
    }

    [Fact(DisplayName = "Las fechas sin zona se serializan como UTC (con Z) y las fechas solo-día no se alteran")]
    public void Fechas_SeSerializanEnUtc()
    {
        var opciones = new JsonSerializerOptions();
        opciones.Converters.Add(new UtcDateTimeJsonConverter());

        var sinZona = new DateTime(2026, 10, 15, 20, 28, 0, DateTimeKind.Unspecified);
        Assert.Equal("\"2026-10-15T20:28:00Z\"", JsonSerializer.Serialize(sinZona, opciones));

        var utc = new DateTime(2026, 10, 15, 20, 28, 0, DateTimeKind.Utc);
        Assert.Equal("\"2026-10-15T20:28:00Z\"", JsonSerializer.Serialize(utc, opciones));

        var soloDia = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal("\"2026-10-15T00:00:00\"", JsonSerializer.Serialize(soloDia, opciones));

        // también aplica a las fechas opcionales y se puede volver a leer
        DateTime? opcional = sinZona;
        Assert.Equal("\"2026-10-15T20:28:00Z\"", JsonSerializer.Serialize(opcional, opciones));
        Assert.Equal(utc, JsonSerializer.Deserialize<DateTime>("\"2026-10-15T20:28:00Z\"", opciones));
    }
}
