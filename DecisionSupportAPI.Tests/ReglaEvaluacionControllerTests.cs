using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de ReglaEvaluacionController (RF07, RF08, RF13).</summary>
public class ReglaEvaluacionControllerTests
{
    private static ReglaEvaluacionController NewController(ApplicationDbContext ctx, System.Security.Claims.ClaimsPrincipal user)
    {
        var controller = new ReglaEvaluacionController(ctx, TestHelpers.NewAuditoriaService(ctx, user), new ProyectoAccesoService(ctx));
        TestHelpers.SetUser(controller, user);
        return controller;
    }

    private static async Task<Proyecto> SeedConReglaGlobalAsync(ApplicationDbContext ctx)
    {
        var p = new Proyecto { Nombre = "P", Estado = "activo" };
        ctx.Proyectos.Add(p);
        ctx.ReglasEvaluacion.Add(new ReglaEvaluacion { Nombre = "Cobertura mínima", Criterio = "cobertura", Umbral = 80m, Estado = "activo", ProyectoId = null });
        await ctx.SaveChangesAsync();
        return p;
    }

    [Fact(DisplayName = "GetByProyecto sin override devuelve el umbral global como vigente")]
    public async Task GetByProyecto_SinOverride_UsaGlobal()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedConReglaGlobalAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_evaluacion" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetByProyecto(proyecto.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        // El controller devuelve una lista de objetos anónimos (internos al
        // ensamblado del backend) — se leen por reflexión en vez de "dynamic"
        // para no depender de InternalsVisibleTo entre ensamblados.
        var lista = (System.Collections.IEnumerable)ok.Value!;
        var encontrado = false;
        foreach (var item in lista)
        {
            var umbral = (decimal?)item.GetType().GetProperty("Umbral")!.GetValue(item);
            var esPersonalizado = (bool)item.GetType().GetProperty("EsPersonalizado")!.GetValue(item)!;
            if (umbral == 80m && !esPersonalizado) { encontrado = true; break; }
        }
        Assert.True(encontrado, "Se esperaba encontrar el criterio 'cobertura' con el umbral global (80) sin personalizar.");
    }

    [Fact(DisplayName = "RF13: GetByProyecto sin acceso al proyecto devuelve 403")]
    public async Task GetByProyecto_SinAcceso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedConReglaGlobalAsync(ctx);
        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_evaluacion" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.GetByProyecto(proyecto.Id);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact(DisplayName = "UpsertProyecto crea un override personalizado para el proyecto")]
    public async Task UpsertProyecto_CreaOverride()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedConReglaGlobalAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_reglas" });
        var controller = NewController(ctx, admin);

        var result = await controller.UpsertProyecto(proyecto.Id, new UpsertReglaProyectoRequest("cobertura", 95m));

        Assert.IsType<OkObjectResult>(result);
        Assert.Contains(ctx.ReglasEvaluacion, r => r.ProyectoId == proyecto.Id && r.Umbral == 95m);
    }

    [Fact(DisplayName = "UpsertProyecto con umbral fuera de rango devuelve 400")]
    public async Task UpsertProyecto_UmbralFueraDeRango_BadRequest()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedConReglaGlobalAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_reglas" });
        var controller = NewController(ctx, admin);

        var result = await controller.UpsertProyecto(proyecto.Id, new UpsertReglaProyectoRequest("cobertura", -5m));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "DeleteOverrideProyecto sobre un criterio sin override devuelve 404")]
    public async Task DeleteOverrideProyecto_SinOverride_NotFound()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedConReglaGlobalAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_reglas" });
        var controller = NewController(ctx, admin);

        var result = await controller.DeleteOverrideProyecto(proyecto.Id, "cobertura");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact(DisplayName = "GetAll devuelve solo las reglas globales activas")]
    public async Task GetAll_DevuelveReglasGlobales()
    {
        var ctx = TestHelpers.NewContext();
        await SeedConReglaGlobalAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_evaluacion" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result);
        var lista = (System.Collections.IEnumerable)ok.Value!;
        Assert.Single(lista.Cast<object>());
    }

    [Fact(DisplayName = "GetById devuelve la regla global por id")]
    public async Task GetById_Global_DevuelveRegla()
    {
        var ctx = TestHelpers.NewContext();
        await SeedConReglaGlobalAsync(ctx);
        var reglaId = ctx.ReglasEvaluacion.First().Id;
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_evaluacion" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetById(reglaId);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact(DisplayName = "GetById inexistente devuelve 404")]
    public async Task GetById_Inexistente_NotFound()
    {
        var ctx = TestHelpers.NewContext();
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_evaluacion" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetById(999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact(DisplayName = "RF13: GetById de una regla de proyecto sin acceso devuelve 403")]
    public async Task GetById_ReglaDeProyectoSinAcceso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedConReglaGlobalAsync(ctx);
        var reglaProyecto = new ReglaEvaluacion { Nombre = "Cobertura P", Criterio = "cobertura", Umbral = 90m, Estado = "activo", ProyectoId = proyecto.Id };
        ctx.ReglasEvaluacion.Add(reglaProyecto);
        await ctx.SaveChangesAsync();
        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_evaluacion" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.GetById(reglaProyecto.Id);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact(DisplayName = "RF07/RF08: UpdateUmbral actualiza el valor y audita el cambio")]
    public async Task UpdateUmbral_ActualizaValor()
    {
        var ctx = TestHelpers.NewContext();
        await SeedConReglaGlobalAsync(ctx);
        var reglaId = ctx.ReglasEvaluacion.First().Id;
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_reglas" });
        var controller = NewController(ctx, admin);

        var result = await controller.UpdateUmbral(reglaId, new UpdateUmbralRequest(85m, null));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(85m, ctx.ReglasEvaluacion.First(r => r.Id == reglaId).Umbral);
        Assert.True(ctx.Auditoria.Any(a => a.EntidadAfectada == "ReglaEvaluacion"));
    }

    [Fact(DisplayName = "UpdateUmbral fuera de rango devuelve 400")]
    public async Task UpdateUmbral_FueraDeRango_BadRequest()
    {
        var ctx = TestHelpers.NewContext();
        await SeedConReglaGlobalAsync(ctx);
        var reglaId = ctx.ReglasEvaluacion.First().Id;
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_reglas" });
        var controller = NewController(ctx, admin);

        var result = await controller.UpdateUmbral(reglaId, new UpdateUmbralRequest(-1m, null));

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
