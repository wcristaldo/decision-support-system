using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de VersionesController (RF02, RF13).</summary>
public class VersionesControllerTests
{
    private static VersionesController NewController(DecisionSupportAPI.Data.ApplicationDbContext ctx, System.Security.Claims.ClaimsPrincipal user)
    {
        var controller = new VersionesController(ctx, TestHelpers.NewAuditoriaService(ctx, user), new ProyectoAccesoService(ctx));
        TestHelpers.SetUser(controller, user);
        return controller;
    }

    private static async Task<Proyecto> SeedProyectoAsync(DecisionSupportAPI.Data.ApplicationDbContext ctx)
    {
        var p = new Proyecto { Nombre = "Proyecto V", Estado = "activo" };
        ctx.Proyectos.Add(p);
        await ctx.SaveChangesAsync();
        return p;
    }

    [Fact(DisplayName = "GetByProyecto devuelve las versiones del proyecto")]
    public async Task GetByProyecto_DevuelveVersiones()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedProyectoAsync(ctx);
        ctx.Versiones.Add(new Models.Version { ProyectoId = proyecto.Id, NumeroVersion = "1.0.0" });
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetByProyecto(proyecto.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var lista = Assert.IsAssignableFrom<List<VersionDto>>(ok.Value);
        Assert.Single(lista);
    }

    [Fact(DisplayName = "RF13: GetByProyecto sin acceso al proyecto devuelve 403")]
    public async Task GetByProyecto_SinAcceso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedProyectoAsync(ctx);

        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_proyectos" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.GetByProyecto(proyecto.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact(DisplayName = "Create con número de versión válido crea la versión")]
    public async Task Create_NumeroValido_Crea()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedProyectoAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.Create(new CreateVersionDto { ProyectoId = proyecto.Id, NumeroVersion = "2.0.0" });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Single(ctx.Versiones);
    }

    [Fact(DisplayName = "Create con proyecto inexistente devuelve 404")]
    public async Task Create_ProyectoInexistente_NotFound()
    {
        var ctx = TestHelpers.NewContext();
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.Create(new CreateVersionDto { ProyectoId = 999, NumeroVersion = "1.0.0" });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact(DisplayName = "Update con estado inválido devuelve 400")]
    public async Task Update_EstadoInvalido_BadRequest()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedProyectoAsync(ctx);
        var version = new Models.Version { ProyectoId = proyecto.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(version);
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.Update(version.Id, new UpdateVersionDto { Estado = "invalido" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "RF13: Delete sobre una versión de un proyecto no asignado devuelve 403")]
    public async Task Delete_SinAcceso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedProyectoAsync(ctx);
        var version = new Models.Version { ProyectoId = proyecto.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(version);
        await ctx.SaveChangesAsync();

        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "gestionar_proyectos" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.Delete(version.Id);

        Assert.IsType<ForbidResult>(result);
        Assert.Single(ctx.Versiones);
    }

    [Fact(DisplayName = "GetById devuelve la version con acceso al proyecto")]
    public async Task GetById_ConAcceso_DevuelveVersion()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedProyectoAsync(ctx);
        var version = new Models.Version { ProyectoId = proyecto.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(version);
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetById(version.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(version.Id, Assert.IsType<VersionDto>(ok.Value).Id);
    }

    [Fact(DisplayName = "GetById inexistente devuelve 404")]
    public async Task GetById_Inexistente_NotFound()
    {
        var ctx = TestHelpers.NewContext();
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetById(999);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact(DisplayName = "RF13: GetById sin acceso al proyecto devuelve 403")]
    public async Task GetById_SinAcceso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedProyectoAsync(ctx);
        var version = new Models.Version { ProyectoId = proyecto.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(version);
        await ctx.SaveChangesAsync();

        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_proyectos" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.GetById(version.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact(DisplayName = "Update con numero valido actualiza la version y audita el cambio")]
    public async Task Update_NumeroValido_Actualiza()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedProyectoAsync(ctx);
        var version = new Models.Version { ProyectoId = proyecto.Id, NumeroVersion = "1.0.0" };
        ctx.Versiones.Add(version);
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.Update(version.Id, new UpdateVersionDto { NumeroVersion = "1.0.1", Estado = "en_evaluacion" });

        Assert.IsType<NoContentResult>(result);
        var actualizada = ctx.Versiones.First(v => v.Id == version.Id);
        Assert.Equal("1.0.1", actualizada.NumeroVersion);
        Assert.Equal("en_evaluacion", actualizada.Estado);
    }

    [Fact(DisplayName = "Update con numero duplicado en el mismo proyecto devuelve 400")]
    public async Task Update_NumeroDuplicado_BadRequest()
    {
        var ctx = TestHelpers.NewContext();
        var proyecto = await SeedProyectoAsync(ctx);
        var v1 = new Models.Version { ProyectoId = proyecto.Id, NumeroVersion = "1.0.0" };
        var v2 = new Models.Version { ProyectoId = proyecto.Id, NumeroVersion = "2.0.0" };
        ctx.Versiones.AddRange(v1, v2);
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.Update(v2.Id, new UpdateVersionDto { NumeroVersion = "1.0.0" });

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
