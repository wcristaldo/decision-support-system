using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de ProyectosController (RF01, RF13). Los tests de
/// RF13 (filtro por proyecto asignado) verifican el mismo comportamiento que
/// ProyectoAccesoService, a través del controller real.</summary>
public class ProyectosControllerTests
{
    private static ProyectosController NewController(DecisionSupportAPI.Data.ApplicationDbContext ctx, System.Security.Claims.ClaimsPrincipal user)
    {
        var controller = new ProyectosController(ctx, TestHelpers.NewAuditoriaService(ctx, user), new FakeSuscripcionService(), new ProyectoAccesoService(ctx));
        TestHelpers.SetUser(controller, user);
        return controller;
    }

    [Fact(DisplayName = "GetAll como Administrador devuelve todos los proyectos sin restricción")]
    public async Task GetAll_Administrador_DevuelveTodos()
    {
        var ctx = TestHelpers.NewContext();
        ctx.Proyectos.AddRange(
            new Proyecto { Nombre = "P1", Estado = "activo" },
            new Proyecto { Nombre = "P2", Estado = "activo" });
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var lista = Assert.IsAssignableFrom<List<ProyectoDto>>(ok.Value);
        Assert.Equal(2, lista.Count);
    }

    [Fact(DisplayName = "RF13: GetAll para un usuario sin proyectos asignados devuelve lista vacía")]
    public async Task GetAll_UsuarioSinProyectosAsignados_ListaVacia()
    {
        var ctx = TestHelpers.NewContext();
        ctx.Proyectos.Add(new Proyecto { Nombre = "P1", Estado = "activo" });
        await ctx.SaveChangesAsync();

        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_proyectos" }, proyectoIds: Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var lista = Assert.IsAssignableFrom<List<ProyectoDto>>(ok.Value);
        Assert.Empty(lista);
    }

    [Fact(DisplayName = "RF13: GetAll para un usuario con proyectos asignados devuelve solo esos")]
    public async Task GetAll_UsuarioConProyectosAsignados_SoloEsos()
    {
        var ctx = TestHelpers.NewContext();
        var p1 = new Proyecto { Nombre = "P1", Estado = "activo" };
        var p2 = new Proyecto { Nombre = "P2", Estado = "activo" };
        ctx.Proyectos.AddRange(p1, p2);
        await ctx.SaveChangesAsync();

        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_proyectos" }, new[] { p1.Id });
        var controller = NewController(ctx, analista);

        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var lista = Assert.IsAssignableFrom<List<ProyectoDto>>(ok.Value);
        Assert.Single(lista);
        Assert.Equal(p1.Id, lista[0].Id);
    }

    [Fact(DisplayName = "RF13: GetById sobre un proyecto no asignado devuelve 403 Forbid")]
    public async Task GetById_ProyectoNoAsignado_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var p1 = new Proyecto { Nombre = "P1", Estado = "activo" };
        ctx.Proyectos.Add(p1);
        await ctx.SaveChangesAsync();

        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_proyectos" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.GetById(p1.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact(DisplayName = "GetById con id inexistente devuelve 404")]
    public async Task GetById_Inexistente_NotFound()
    {
        var ctx = TestHelpers.NewContext();
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetById(999);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact(DisplayName = "Create con datos válidos crea el proyecto y su versión inicial en una sola transacción")]
    public async Task Create_DatosValidos_CreaProyectoYVersionInicial()
    {
        var ctx = TestHelpers.NewContext();
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.Create(new CreateProyectoDto { Nombre = "Nuevo Proyecto", VersionInicial = "1.0.0" });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Single(ctx.Proyectos);
        Assert.Single(ctx.Versiones);
        Assert.Equal("1.0.0", ctx.Versiones.First().NumeroVersion);
    }

    [Fact(DisplayName = "Update con estado inválido devuelve 400")]
    public async Task Update_EstadoInvalido_BadRequest()
    {
        var ctx = TestHelpers.NewContext();
        var p1 = new Proyecto { Nombre = "P1", Estado = "activo" };
        ctx.Proyectos.Add(p1);
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.Update(p1.Id, new UpdateProyectoDto { Estado = "no_valido" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "RF13: Delete sobre un proyecto no asignado devuelve 403 Forbid, no lo elimina")]
    public async Task Delete_ProyectoNoAsignado_ForbidNoElimina()
    {
        var ctx = TestHelpers.NewContext();
        var p1 = new Proyecto { Nombre = "P1", Estado = "activo" };
        ctx.Proyectos.Add(p1);
        await ctx.SaveChangesAsync();

        var lider = TestHelpers.BuildUser(3, new[] { "Líder Técnico" }, new[] { "gestionar_proyectos" }, Array.Empty<int>());
        var controller = NewController(ctx, lider);

        var result = await controller.Delete(p1.Id);

        Assert.IsType<ForbidResult>(result);
        Assert.Single(ctx.Proyectos);
    }

    [Fact(DisplayName = "GetById con acceso devuelve el proyecto")]
    public async Task GetById_ConAcceso_DevuelveProyecto()
    {
        var ctx = TestHelpers.NewContext();
        var p1 = new Proyecto { Nombre = "P1", Estado = "activo" };
        ctx.Proyectos.Add(p1);
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetById(p1.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(p1.Id, Assert.IsType<ProyectoDto>(ok.Value).Id);
    }

    [Fact(DisplayName = "GetVersiones devuelve las versiones del proyecto con acceso")]
    public async Task GetVersiones_ConAcceso_DevuelveVersiones()
    {
        var ctx = TestHelpers.NewContext();
        var p1 = new Proyecto { Nombre = "P1", Estado = "activo" };
        ctx.Proyectos.Add(p1);
        await ctx.SaveChangesAsync();
        ctx.Versiones.Add(new Models.Version { ProyectoId = p1.Id, NumeroVersion = "1.0.0" });
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetVersiones(p1.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(Assert.IsAssignableFrom<List<VersionDto>>(ok.Value));
    }

    [Fact(DisplayName = "GetVersiones con proyecto inexistente devuelve 404")]
    public async Task GetVersiones_Inexistente_NotFound()
    {
        var ctx = TestHelpers.NewContext();
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetVersiones(999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact(DisplayName = "RF13: GetVersiones sin acceso al proyecto devuelve 403")]
    public async Task GetVersiones_SinAcceso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var p1 = new Proyecto { Nombre = "P1", Estado = "activo" };
        ctx.Proyectos.Add(p1);
        await ctx.SaveChangesAsync();

        var analista = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_proyectos" }, Array.Empty<int>());
        var controller = NewController(ctx, analista);

        var result = await controller.GetVersiones(p1.Id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact(DisplayName = "Update con datos validos actualiza el proyecto y audita el cambio")]
    public async Task Update_DatosValidos_Actualiza()
    {
        var ctx = TestHelpers.NewContext();
        var p1 = new Proyecto { Nombre = "P1", Estado = "activo" };
        ctx.Proyectos.Add(p1);
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.Update(p1.Id, new UpdateProyectoDto { Nombre = "P1 renombrado", Estado = "inactivo" });

        Assert.IsType<NoContentResult>(result);
        var actualizado = ctx.Proyectos.First(p => p.Id == p1.Id);
        Assert.Equal("P1 renombrado", actualizado.Nombre);
        Assert.Equal("inactivo", actualizado.Estado);
    }

    [Fact(DisplayName = "Update con nombre duplicado devuelve 400")]
    public async Task Update_NombreDuplicado_BadRequest()
    {
        var ctx = TestHelpers.NewContext();
        var p1 = new Proyecto { Nombre = "P1", Estado = "activo" };
        var p2 = new Proyecto { Nombre = "P2", Estado = "activo" };
        ctx.Proyectos.AddRange(p1, p2);
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_proyectos" });
        var controller = NewController(ctx, admin);

        var result = await controller.Update(p2.Id, new UpdateProyectoDto { Nombre = "P1" });

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
