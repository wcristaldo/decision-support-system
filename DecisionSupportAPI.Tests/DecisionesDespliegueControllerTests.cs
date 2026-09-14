using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de DecisionesDespliegueController (RF12, RF13).</summary>
public class DecisionesDespliegueControllerTests
{
    private static DecisionesDespliegueController NewController(ApplicationDbContext ctx, System.Security.Claims.ClaimsPrincipal user)
    {
        var controller = new DecisionesDespliegueController(ctx, TestHelpers.NewAuditoriaService(ctx, user), new ActaPdfService(), new ProyectoAccesoService(ctx));
        TestHelpers.SetUser(controller, user);
        return controller;
    }

    private static async Task<(Models.Version version, int recomendacionId)> SeedRecomendacionAsync(ApplicationDbContext ctx, string tipoRecomendacion)
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
        var eval = new Evaluacion { ResultadoId = r.Id, FechaEvaluacion = DateTime.UtcNow };
        ctx.Evaluaciones.Add(eval);
        await ctx.SaveChangesAsync();
        var rec = new Recomendacion { EvaluacionId = eval.Id, TipoRecomendacion = tipoRecomendacion, FechaGeneracion = DateTime.UtcNow };
        ctx.Recomendaciones.Add(rec);
        await ctx.SaveChangesAsync();
        return (v, rec.Id);
    }

    [Fact(DisplayName = "Create con comentario vacío devuelve 400 (RF12: justificación obligatoria)")]
    public async Task Create_ComentarioVacio_BadRequest()
    {
        var ctx = TestHelpers.NewContext();
        var (_, recId) = await SeedRecomendacionAsync(ctx, "desplegar");
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "registrar_decision" });
        var controller = NewController(ctx, admin);

        var result = await controller.Create(new CreateDecisionDespliegueDto { RecomendacionId = recId, DecisionFinal = "aprobado", Comentario = "   " });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact(DisplayName = "Create detecta override cuando se aprueba algo 'no_desplegar' y exige 20+ caracteres")]
    public async Task Create_OverrideConJustificacionCorta_Rechaza()
    {
        var ctx = TestHelpers.NewContext();
        var (_, recId) = await SeedRecomendacionAsync(ctx, "no_desplegar");
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "registrar_decision" });
        var controller = NewController(ctx, admin);

        var result = await controller.Create(new CreateDecisionDespliegueDto { RecomendacionId = recId, DecisionFinal = "aprobado", Comentario = "ok" });

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var codigo = (string)bad.Value!.GetType().GetProperty("codigo")!.GetValue(bad.Value)!;
        Assert.Equal("JUSTIFICACION_INSUFICIENTE_OVERRIDE", codigo);
    }

    [Fact(DisplayName = "Create con override y justificación suficiente marca EsOverride=true")]
    public async Task Create_OverrideConJustificacionSuficiente_MarcaOverride()
    {
        var ctx = TestHelpers.NewContext();
        var (_, recId) = await SeedRecomendacionAsync(ctx, "no_desplegar");
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "registrar_decision" });
        var controller = NewController(ctx, admin);

        var result = await controller.Create(new CreateDecisionDespliegueDto
        {
            RecomendacionId = recId, DecisionFinal = "aprobado",
            Comentario = "Se aprueba por acuerdo excepcional con el cliente pese a las fallas."
        });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.True(ctx.DecisionesDespliegue.First().EsOverride);
    }

    [Fact(DisplayName = "Create sin override (decisión alineada) no marca EsOverride")]
    public async Task Create_SinOverride_NoMarcaOverride()
    {
        var ctx = TestHelpers.NewContext();
        var (_, recId) = await SeedRecomendacionAsync(ctx, "desplegar");
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "registrar_decision" });
        var controller = NewController(ctx, admin);

        var result = await controller.Create(new CreateDecisionDespliegueDto
        {
            RecomendacionId = recId, DecisionFinal = "aprobado",
            Comentario = "Aprobado según lo esperado, todo en regla."
        });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.False(ctx.DecisionesDespliegue.First().EsOverride);
    }

    [Fact(DisplayName = "RF13: Create sin acceso al proyecto de la recomendación devuelve 403")]
    public async Task Create_SinAcceso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var (_, recId) = await SeedRecomendacionAsync(ctx, "desplegar");
        var analista = TestHelpers.BuildUser(2, new[] { "Líder Técnico" }, new[] { "registrar_decision" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.Create(new CreateDecisionDespliegueDto { RecomendacionId = recId, DecisionFinal = "aprobado", Comentario = "Comentario suficientemente largo." });

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Empty(ctx.DecisionesDespliegue);
    }

    [Fact(DisplayName = "GetAdherencia calcula el porcentaje excluyendo decisiones 'postergado'")]
    public async Task GetAdherencia_ExcluyePostergadas()
    {
        var ctx = TestHelpers.NewContext();
        var (_, recId1) = await SeedRecomendacionAsync(ctx, "desplegar");
        ctx.DecisionesDespliegue.Add(new DecisionDespliegue { RecomendacionId = recId1, DecisionFinal = "aprobado", Comentario = "ok", EsOverride = false });
        var (_, recId2) = await SeedRecomendacionAsync(ctx, "no_desplegar");
        ctx.DecisionesDespliegue.Add(new DecisionDespliegue { RecomendacionId = recId2, DecisionFinal = "postergado", Comentario = "esperar", EsOverride = false });
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_decisiones" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetAdherencia();

        var ok = Assert.IsType<OkObjectResult>(result);
        var total = (int)ok.Value!.GetType().GetProperty("total")!.GetValue(ok.Value)!;
        Assert.Equal(1, total); // postergado no cuenta
    }
}
