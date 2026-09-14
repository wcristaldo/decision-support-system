using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

file class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "DecisionSupportAPI.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
}

/// <summary>RNF08: cobertura de AuthController (RNF01 — login/JWT).</summary>
public class AuthControllerTests
{
    private static (ApplicationDbContext ctx, AuthController controller) Arrange()
    {
        var ctx = TestHelpers.NewContext();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "clave-de-prueba-para-tests-32-caracteres-o-mas",
            ["Jwt:Issuer"] = "Tests", ["Jwt:Audience"] = "Tests", ["Jwt:ExpirationMinutes"] = "60",
        }).Build();
        var auth = new AuthenticationService(ctx, config);
        var controller = new AuthController(auth, ctx, TestHelpers.NewAuditoriaService(ctx), new FakeHostEnvironment(), NullLogger<AuthController>.Instance);
        return (ctx, controller);
    }

    private static async Task SeedUsuarioAsync(ApplicationDbContext ctx, AuthController controller, string email, string password)
    {
        var usuario = new Usuario
        {
            Nombre = "Test", Email = email,
            PasswordHash = new AuthenticationService(ctx, new ConfigurationBuilder().Build()).HashPassword(password),
            Estado = "activo",
        };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
    }

    [Fact(DisplayName = "Login con credenciales válidas devuelve 200 con el token")]
    public async Task Login_CredencialesValidas_Devuelve200()
    {
        var (ctx, controller) = Arrange();
        await SeedUsuarioAsync(ctx, controller, "user@test.com", "Passw0rd!23");

        var result = await controller.Login(new LoginRequestDto { Email = "user@test.com", Password = "Passw0rd!23" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact(DisplayName = "Login con credenciales inválidas devuelve 401, sin revelar si el email existe")]
    public async Task Login_CredencialesInvalidas_Devuelve401()
    {
        var (_, controller) = Arrange();

        var result = await controller.Login(new LoginRequestDto { Email = "no-existe@test.com", Password = "cualquiera" });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact(DisplayName = "ChangePassword con nueva contraseña y confirmación distintas devuelve 400")]
    public async Task ChangePassword_ConfirmacionNoCoincide_BadRequest()
    {
        var (ctx, controller) = Arrange();
        await SeedUsuarioAsync(ctx, controller, "user@test.com", "Passw0rd!23");
        var usuario = ctx.Usuarios.First();
        TestHelpers.SetUser(controller, TestHelpers.BuildUser(usuario.IdUsuario, new[] { "Analista QA" }, Array.Empty<string>()));

        var result = await controller.ChangePassword(new ChangePasswordRequestDto
        {
            CurrentPassword = "Passw0rd!23", NewPassword = "NuevaPass1!", ConfirmPassword = "Distinta1!"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "Me devuelve el nombre, email, roles y permisos vigentes del claim actual")]
    public void Me_DevuelveDatosDelUsuarioActual()
    {
        var (_, controller) = Arrange();
        var user = TestHelpers.BuildUser(1, new[] { "Analista QA" }, new[] { "ver_proyectos" });
        TestHelpers.SetUser(controller, user);

        var result = controller.Me();

        var ok = Assert.IsType<OkObjectResult>(result);
        var roles = (List<string>)ok.Value!.GetType().GetProperty("roles")!.GetValue(ok.Value)!;
        Assert.Contains("Analista QA", roles);
    }

    [Fact(DisplayName = "ChangePassword con contraseña actual incorrecta devuelve 400")]
    public async Task ChangePassword_ActualIncorrecta_BadRequest()
    {
        var (ctx, controller) = Arrange();
        await SeedUsuarioAsync(ctx, controller, "user@test.com", "Passw0rd!23");
        var usuario = ctx.Usuarios.First();
        TestHelpers.SetUser(controller, TestHelpers.BuildUser(usuario.IdUsuario, new[] { "Analista QA" }, Array.Empty<string>()));

        var result = await controller.ChangePassword(new ChangePasswordRequestDto
        {
            CurrentPassword = "incorrecta", NewPassword = "NuevaPass1!", ConfirmPassword = "NuevaPass1!"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "ResetPassword sin permiso gestionar_usuarios devuelve 403")]
    public async Task ResetPassword_SinPermiso_Forbid403()
    {
        var (ctx, controller) = Arrange();
        await SeedUsuarioAsync(ctx, controller, "user@test.com", "Passw0rd!23");
        var usuario = ctx.Usuarios.First();
        TestHelpers.SetUser(controller, TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_usuarios" }));

        var result = await controller.ResetPassword(usuario.IdUsuario, new ResetPasswordDto { NewPassword = "NuevaPass1!" });

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, status.StatusCode);
    }
}
