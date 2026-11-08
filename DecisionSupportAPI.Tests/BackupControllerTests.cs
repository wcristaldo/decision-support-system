using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08/RNF12: cobertura de BackupController (respaldo periódico
/// de la base de datos). EjecutarAhora dispara un pg_dump real vía
/// DatabaseBackupService y queda fuera de este set: no es unitariamente
/// testeable sin un proceso pg_dump real disponible en el entorno de CI.</summary>
public class BackupControllerTests
{
    private static BackupController NewController(ApplicationDbContext ctx, System.Security.Claims.ClaimsPrincipal user)
    {
        var backupService = new DatabaseBackupService(null!, new ConfigurationBuilder().Build(), NullLogger<DatabaseBackupService>.Instance);
        var controller = new BackupController(ctx, backupService, TestHelpers.NewAuditoriaService(ctx, user));
        TestHelpers.SetUser(controller, user);
        return controller;
    }

    private static System.Security.Claims.ClaimsPrincipal Admin() =>
        TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_usuarios" });

    [Fact(DisplayName = "GetConfiguracion sin configuracion inicializada devuelve 404")]
    public async Task GetConfiguracion_SinInicializar_NotFound()
    {
        var ctx = TestHelpers.NewContext();
        var controller = NewController(ctx, Admin());

        var result = await controller.GetConfiguracion();

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact(DisplayName = "GetConfiguracion devuelve la configuracion existente")]
    public async Task GetConfiguracion_Existente_DevuelveDatos()
    {
        var ctx = TestHelpers.NewContext();
        ctx.ConfiguracionesBackup.Add(new ConfiguracionBackup { IntervaloHoras = 24, CarpetaDestino = "backups", Activo = true });
        await ctx.SaveChangesAsync();
        var controller = NewController(ctx, Admin());

        var result = await controller.GetConfiguracion();

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact(DisplayName = "ActualizarConfiguracion con intervalo invalido devuelve 400")]
    public async Task ActualizarConfiguracion_IntervaloInvalido_BadRequest()
    {
        var ctx = TestHelpers.NewContext();
        ctx.ConfiguracionesBackup.Add(new ConfiguracionBackup { IntervaloHoras = 24, CarpetaDestino = "backups", Activo = true });
        await ctx.SaveChangesAsync();
        var controller = NewController(ctx, Admin());

        var result = await controller.ActualizarConfiguracion(new BackupController.ConfiguracionBackupRequest(0, "backups", true));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "ActualizarConfiguracion con datos validos guarda los cambios y audita")]
    public async Task ActualizarConfiguracion_DatosValidos_Guarda()
    {
        var ctx = TestHelpers.NewContext();
        ctx.ConfiguracionesBackup.Add(new ConfiguracionBackup { IntervaloHoras = 24, CarpetaDestino = "backups", Activo = true });
        await ctx.SaveChangesAsync();
        var controller = NewController(ctx, Admin());

        var result = await controller.ActualizarConfiguracion(new BackupController.ConfiguracionBackupRequest(12, "otra-carpeta", false));

        Assert.IsType<OkObjectResult>(result);
        var config = ctx.ConfiguracionesBackup.First();
        Assert.Equal(12, config.IntervaloHoras);
        Assert.Equal("otra-carpeta", config.CarpetaDestino);
        Assert.False(config.Activo);
        Assert.True(ctx.Auditoria.Any(a => a.EntidadAfectada == "ConfiguracionBackup"));
    }

    [Fact(DisplayName = "GetHistorial sin configuracion inicializada devuelve lista vacia")]
    public async Task GetHistorial_SinConfiguracion_ListaVacia()
    {
        var ctx = TestHelpers.NewContext();
        var controller = NewController(ctx, Admin());

        var result = await controller.GetHistorial();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Empty((System.Collections.IEnumerable)ok.Value!);
    }

    [Fact(DisplayName = "GetHistorial y Descargar: carpeta inexistente devuelve lista vacia y 404; carpeta con respaldos lista y sirve el archivo")]
    public async Task GetHistorial_CarpetaInexistente_ListaVacia()
    {
        var ctx = TestHelpers.NewContext();
        var carpeta = Path.Combine(Path.GetTempPath(), "dss-backups-" + Guid.NewGuid());
        ctx.ConfiguracionesBackup.Add(new ConfiguracionBackup { IntervaloHoras = 24, CarpetaDestino = carpeta, Activo = true });
        await ctx.SaveChangesAsync();
        var controller = NewController(ctx, Admin());

        // la carpeta todavía no existe
        var ok = Assert.IsType<OkObjectResult>(await controller.GetHistorial());
        Assert.Empty((System.Collections.IEnumerable)ok.Value!);
        Assert.IsType<NotFoundObjectResult>(await controller.Descargar("backup_20261015_203236.sql"));

        // nombres que no son un respaldo (o intentan salir de la carpeta) se rechazan
        foreach (var malo in new[] { "../secreto.sql", "backup_1.txt", "otro.sql", "backup_..\\x.sql" })
            Assert.IsType<BadRequestObjectResult>(await controller.Descargar(malo));

        // con la carpeta creada y un respaldo dentro, se lista y se puede descargar
        Directory.CreateDirectory(carpeta);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(carpeta, "backup_20261015_203236.sql"), "-- dump");
            var lista = Assert.IsType<OkObjectResult>(await controller.GetHistorial());
            Assert.Single((System.Collections.IEnumerable)lista.Value!);

            var file = Assert.IsType<PhysicalFileResult>(await controller.Descargar("backup_20261015_203236.sql"));
            Assert.Equal("backup_20261015_203236.sql", file.FileDownloadName);
            Assert.True(ctx.Auditoria.Any(a => a.Accion == "Download" && a.EntidadAfectada == "ConfiguracionBackup"));

            // un respaldo que no existe en la carpeta devuelve 404
            Assert.IsType<NotFoundObjectResult>(await controller.Descargar("backup_20000101_000000.sql"));
        }
        finally
        {
            Directory.Delete(carpeta, true);
        }
    }
}
