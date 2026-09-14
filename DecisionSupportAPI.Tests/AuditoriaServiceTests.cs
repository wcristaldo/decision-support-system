using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de AuditoriaService (RNF05 — log de auditoría de
/// acciones relevantes con usuario, acción, fecha y hora).</summary>
public class AuditoriaServiceTests
{
    private static (ApplicationDbContext ctx, AuditoriaService service, DefaultHttpContext httpContext) Arrange(int? usuarioId = 1)
    {
        var ctx = TestHelpers.NewContext();
        var httpContext = new DefaultHttpContext();
        if (usuarioId != null)
            httpContext.User = TestHelpers.BuildUser(usuarioId.Value, new[] { "Administrador" }, new[] { "gestionar_usuarios" });
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var service = new AuditoriaService(ctx, accessor, NullLogger<AuditoriaService>.Instance);
        return (ctx, service, httpContext);
    }

    [Fact(DisplayName = "RegistrarAsync persiste usuario, acción, entidad e IP")]
    public async Task RegistrarAsync_PersisteRegistroCompleto()
    {
        var (ctx, service, _) = Arrange();

        await service.RegistrarAsync("Create", "Proyecto", 5, "Proyecto creado: Demo");

        var registro = Assert.Single(ctx.Auditoria);
        Assert.Equal(1, registro.UsuarioId);
        Assert.Equal("Create", registro.Accion);
        Assert.Equal("Proyecto", registro.EntidadAfectada);
        Assert.Equal(5, registro.IdRegistroAfectado);
        Assert.Equal("127.0.0.1", registro.IpOrigen);
    }

    [Fact(DisplayName = "RegistrarAsync trunca el detalle a 255 caracteres (límite de la columna)")]
    public async Task RegistrarAsync_TruncaDetalleLargo()
    {
        var (ctx, service, _) = Arrange();
        var detalleLargo = new string('x', 400);

        await service.RegistrarAsync("Update", "Usuario", 1, detalleLargo);

        var registro = Assert.Single(ctx.Auditoria);
        Assert.Equal(255, registro.Detalle!.Length);
    }

    [Fact(DisplayName = "RegistrarAsync usa usuarioIdExplicito cuando el request aún no tiene claims (ej. Login)")]
    public async Task RegistrarAsync_UsuarioIdExplicito_SinClaimsEnRequest()
    {
        var (ctx, service, _) = Arrange(usuarioId: null);

        await service.RegistrarAsync("Login", "Usuario", 9, "Login exitoso", usuarioIdExplicito: 9);

        var registro = Assert.Single(ctx.Auditoria);
        Assert.Equal(9, registro.UsuarioId);
    }

    [Fact(DisplayName = "RegistrarAsync no lanza excepción si no hay HttpContext disponible")]
    public async Task RegistrarAsync_SinHttpContext_NoLanza()
    {
        var ctx = TestHelpers.NewContext();
        var accessor = new HttpContextAccessor { HttpContext = null };
        var service = new AuditoriaService(ctx, accessor, NullLogger<AuditoriaService>.Instance);

        await service.RegistrarAsync("Create", "Proyecto", 1, "detalle");

        Assert.Empty(ctx.Auditoria);
    }
}
