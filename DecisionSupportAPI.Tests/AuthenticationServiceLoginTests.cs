using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de AuthenticationService.LoginAsync/ChangePasswordAsync
/// (RNF01 — JWT con expiración configurable; RNF04 — bcrypt).</summary>
public class AuthenticationServiceLoginTests
{
    private static (ApplicationDbContext ctx, AuthenticationService service) Arrange()
    {
        var ctx = TestHelpers.NewContext();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "clave-de-prueba-para-tests-32-caracteres-o-mas",
            ["Jwt:Issuer"] = "DecisionSupportAPI.Tests",
            ["Jwt:Audience"] = "DecisionSupportAPI.Tests",
            ["Jwt:ExpirationMinutes"] = "60",
        }).Build();
        var service = new AuthenticationService(ctx, config);
        return (ctx, service);
    }

    private static async Task<Usuario> SeedUsuarioAsync(ApplicationDbContext ctx, AuthenticationService auth, string password, string estado = "activo")
    {
        var rol = new Rol { NombreRol = "Analista QA", Estado = "activo" };
        ctx.Roles.Add(rol);
        await ctx.SaveChangesAsync();

        var permiso = new Permiso { NombrePermiso = "ver_proyectos", Modulo = "Proyectos" };
        ctx.Permisos.Add(permiso);
        await ctx.SaveChangesAsync();
        ctx.RolPermisos.Add(new RolPermiso { IdRol = rol.IdRol, IdPermiso = permiso.IdPermiso });

        var usuario = new Usuario
        {
            Nombre = "Ana", Apellido = "López", Email = "ana@test.com",
            PasswordHash = auth.HashPassword(password), Estado = estado,
        };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();

        ctx.UsuarioRoles.Add(new UsuarioRol { IdUsuario = usuario.IdUsuario, IdRol = rol.IdRol, Estado = "activo" });
        await ctx.SaveChangesAsync();

        return usuario;
    }

    [Fact(DisplayName = "LoginAsync con credenciales válidas devuelve un JWT y los roles/permisos del usuario")]
    public async Task LoginAsync_CredencialesValidas_DevuelveTokenYRoles()
    {
        var (ctx, auth) = Arrange();
        await SeedUsuarioAsync(ctx, auth, "Passw0rd!23");

        var resultado = await auth.LoginAsync(new LoginRequestDto { Email = "ana@test.com", Password = "Passw0rd!23" });

        Assert.NotNull(resultado);
        Assert.False(string.IsNullOrWhiteSpace(resultado!.Token));
        Assert.Contains("Analista QA", resultado.Usuario.Roles);
        Assert.Contains("ver_proyectos", resultado.Usuario.Permisos);
    }

    [Fact(DisplayName = "LoginAsync con contraseña incorrecta devuelve null")]
    public async Task LoginAsync_PasswordIncorrecta_DevuelveNull()
    {
        var (ctx, auth) = Arrange();
        await SeedUsuarioAsync(ctx, auth, "Passw0rd!23");

        var resultado = await auth.LoginAsync(new LoginRequestDto { Email = "ana@test.com", Password = "otra-cosa" });

        Assert.Null(resultado);
    }

    [Fact(DisplayName = "LoginAsync con email inexistente devuelve null (mismo resultado que password incorrecta)")]
    public async Task LoginAsync_EmailInexistente_DevuelveNull()
    {
        var (_, auth) = Arrange();

        var resultado = await auth.LoginAsync(new LoginRequestDto { Email = "no-existe@test.com", Password = "cualquiera" });

        Assert.Null(resultado);
    }

    [Fact(DisplayName = "LoginAsync con usuario inactivo devuelve null aunque la contraseña sea correcta")]
    public async Task LoginAsync_UsuarioInactivo_DevuelveNull()
    {
        var (ctx, auth) = Arrange();
        await SeedUsuarioAsync(ctx, auth, "Passw0rd!23", estado: "inactivo");

        var resultado = await auth.LoginAsync(new LoginRequestDto { Email = "ana@test.com", Password = "Passw0rd!23" });

        Assert.Null(resultado);
    }

    [Fact(DisplayName = "ChangePasswordAsync con la contraseña actual correcta actualiza el hash")]
    public async Task ChangePasswordAsync_ActualCorrecta_ActualizaHash()
    {
        var (ctx, auth) = Arrange();
        var usuario = await SeedUsuarioAsync(ctx, auth, "Passw0rd!23");

        var ok = await auth.ChangePasswordAsync(usuario.IdUsuario, "Passw0rd!23", "NuevaPass1!");

        Assert.True(ok);
        Assert.True(auth.VerifyPassword("NuevaPass1!", (await ctx.Usuarios.FindAsync(usuario.IdUsuario))!.PasswordHash));
    }

    [Fact(DisplayName = "ChangePasswordAsync con la contraseña actual incorrecta no cambia nada")]
    public async Task ChangePasswordAsync_ActualIncorrecta_NoCambia()
    {
        var (ctx, auth) = Arrange();
        var usuario = await SeedUsuarioAsync(ctx, auth, "Passw0rd!23");

        var ok = await auth.ChangePasswordAsync(usuario.IdUsuario, "incorrecta", "NuevaPass1!");

        Assert.False(ok);
        Assert.True(auth.VerifyPassword("Passw0rd!23", (await ctx.Usuarios.FindAsync(usuario.IdUsuario))!.PasswordHash));
    }
}
