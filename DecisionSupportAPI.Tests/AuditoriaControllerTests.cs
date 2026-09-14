using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de AuditoriaController (RNF05).</summary>
public class AuditoriaControllerTests
{
    private static AuditoriaController NewController(ApplicationDbContext ctx)
    {
        var controller = new AuditoriaController(ctx, new FakeSuscripcionService());
        TestHelpers.SetUser(controller, TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "ver_auditoria" }));
        return controller;
    }

    [Fact(DisplayName = "GetAuditoria pagina correctamente y no arroja error con offsets fuera de rango")]
    public async Task GetAuditoria_ClampDefensivo_NoRompeConValoresInvalidos()
    {
        var ctx = TestHelpers.NewContext();
        for (int i = 0; i < 5; i++)
            ctx.Auditoria.Add(new Auditoria { Accion = "Create", EntidadAfectada = "Proyecto", FechaEvento = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
        var controller = NewController(ctx);

        var result = await controller.GetAuditoria(usuario: null, accion: null, entidad: null, fechaDesde: null, fechaHasta: null, pagina: -5, limite: 99999);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact(DisplayName = "GetAuditoria filtra por acción correctamente")]
    public async Task GetAuditoria_FiltraPorAccion()
    {
        var ctx = TestHelpers.NewContext();
        ctx.Auditoria.Add(new Auditoria { Accion = "Create", EntidadAfectada = "Proyecto", FechaEvento = DateTime.UtcNow });
        ctx.Auditoria.Add(new Auditoria { Accion = "Delete", EntidadAfectada = "Proyecto", FechaEvento = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
        var controller = NewController(ctx);

        var result = await controller.GetAuditoria(usuario: null, accion: "create", entidad: null, fechaDesde: null, fechaHasta: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var total = (int)ok.Value!.GetType().GetProperty("total")!.GetValue(ok.Value)!;
        Assert.Equal(1, total);
    }

    [Fact(DisplayName = "GetById con id inexistente devuelve 404")]
    public async Task GetById_Inexistente_NotFound()
    {
        var ctx = TestHelpers.NewContext();
        var controller = NewController(ctx);

        var result = await controller.GetById(999);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact(DisplayName = "GetEstadisticas agrupa correctamente por acción")]
    public async Task GetEstadisticas_AgrupaPorAccion()
    {
        var ctx = TestHelpers.NewContext();
        ctx.Auditoria.Add(new Auditoria { Accion = "Create", EntidadAfectada = "Proyecto", FechaEvento = DateTime.UtcNow });
        ctx.Auditoria.Add(new Auditoria { Accion = "Create", EntidadAfectada = "Usuario", FechaEvento = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
        var controller = NewController(ctx);

        var result = await controller.GetEstadisticas(null, null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var totalEventos = (int)ok.Value!.GetType().GetProperty("totalEventos")!.GetValue(ok.Value)!;
        Assert.Equal(2, totalEventos);
    }
}
