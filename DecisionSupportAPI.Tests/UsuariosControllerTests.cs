using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;
using Microsoft.Extensions.Configuration;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de UsuariosController (RF13, RF14).</summary>
public class UsuariosControllerTests
{
    private static UsuariosController NewController(ApplicationDbContext ctx, System.Security.Claims.ClaimsPrincipal user)
    {
        var auth = new AuthenticationService(ctx, new ConfigurationBuilder().Build());
        var controller = new UsuariosController(ctx, auth, TestHelpers.NewAuditoriaService(ctx, user), new FakeSuscripcionService());
        TestHelpers.SetUser(controller, user);
        return controller;
    }

    private static async Task<Rol> SeedRolAsync(ApplicationDbContext ctx, string nombre = "Analista QA")
    {
        var rol = new Rol { NombreRol = nombre, Estado = "activo" };
        ctx.Roles.Add(rol);
        await ctx.SaveChangesAsync();
        return rol;
    }

    [Fact(DisplayName = "Create con email duplicado (case-insensitive) devuelve 400")]
    public async Task Create_EmailDuplicadoCaseInsensitive_BadRequest()
    {
        var ctx = TestHelpers.NewContext();
        await SeedRolAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_usuarios" });
        var controller = NewController(ctx, admin);
        await controller.Create(new CreateUsuarioRequest("Ana", "ana@test.com", "Passw0rd!23", "Analista QA"));

        var result = await controller.Create(new CreateUsuarioRequest("Otra Ana", "ANA@TEST.COM", "Passw0rd!23", "Analista QA"));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "Create con contraseña corta devuelve 400")]
    public async Task Create_PasswordCorta_BadRequest()
    {
        var ctx = TestHelpers.NewContext();
        await SeedRolAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_usuarios" });
        var controller = NewController(ctx, admin);

        var result = await controller.Create(new CreateUsuarioRequest("Ana", "ana@test.com", "corta", "Analista QA"));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "Create sin permiso gestionar_usuarios devuelve 403")]
    public async Task Create_SinPermiso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        await SeedRolAsync(ctx);
        var soloVer = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_usuarios" });
        var controller = NewController(ctx, soloVer);

        var result = await controller.Create(new CreateUsuarioRequest("Ana", "ana@test.com", "Passw0rd!23", "Analista QA"));

        Assert.IsType<ForbidResult>(result);
    }

    [Fact(DisplayName = "RF13: ActualizarProyectosDeUsuario asigna correctamente los proyectos elegidos")]
    public async Task ActualizarProyectosDeUsuario_AsignaProyectos()
    {
        var ctx = TestHelpers.NewContext();
        var rol = await SeedRolAsync(ctx);
        var usuario = new Usuario { Nombre = "Ana", Email = "ana@test.com", PasswordHash = "x", Estado = "activo" };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        ctx.UsuarioRoles.Add(new UsuarioRol { IdUsuario = usuario.IdUsuario, IdRol = rol.IdRol, Estado = "activo" });
        var p1 = new Proyecto { Nombre = "P1", Estado = "activo" };
        var p2 = new Proyecto { Nombre = "P2", Estado = "activo" };
        ctx.Proyectos.AddRange(p1, p2);
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_usuarios" });
        var controller = NewController(ctx, admin);

        var result = await controller.ActualizarProyectosDeUsuario(usuario.IdUsuario, new UsuariosController.ActualizarProyectosRequest(new List<int> { p1.Id }));

        Assert.IsType<OkObjectResult>(result);
        var asignados = ctx.UsuarioProyectos.Where(up => up.IdUsuario == usuario.IdUsuario).ToList();
        Assert.Single(asignados);
        Assert.Equal(p1.Id, asignados[0].IdProyecto);
    }

    [Fact(DisplayName = "RF13: ActualizarProyectosDeUsuario rechaza asignar proyectos a un Administrador")]
    public async Task ActualizarProyectosDeUsuario_Administrador_BadRequest()
    {
        var ctx = TestHelpers.NewContext();
        var rolAdmin = await SeedRolAsync(ctx, "Administrador");
        var usuario = new Usuario { Nombre = "Admin2", Email = "admin2@test.com", PasswordHash = "x", Estado = "activo" };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        ctx.UsuarioRoles.Add(new UsuarioRol { IdUsuario = usuario.IdUsuario, IdRol = rolAdmin.IdRol, Estado = "activo" });
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_usuarios" });
        var controller = NewController(ctx, admin);

        var result = await controller.ActualizarProyectosDeUsuario(usuario.IdUsuario, new UsuariosController.ActualizarProyectosRequest(new List<int>()));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "RF13: GetProyectosDeUsuario devuelve isAdmin=true y sin restricciones para un Administrador")]
    public async Task GetProyectosDeUsuario_Administrador_IsAdminTrue()
    {
        var ctx = TestHelpers.NewContext();
        var rolAdmin = await SeedRolAsync(ctx, "Administrador");
        var usuario = new Usuario { Nombre = "Admin2", Email = "admin2@test.com", PasswordHash = "x", Estado = "activo" };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        ctx.UsuarioRoles.Add(new UsuarioRol { IdUsuario = usuario.IdUsuario, IdRol = rolAdmin.IdRol, Estado = "activo" });
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_usuarios" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetProyectosDeUsuario(usuario.IdUsuario);

        var ok = Assert.IsType<OkObjectResult>(result);
        var isAdmin = (bool)ok.Value!.GetType().GetProperty("isAdmin")!.GetValue(ok.Value)!;
        Assert.True(isAdmin);
    }

    [Fact(DisplayName = "GetUsuarios devuelve el rol activo de cada usuario")]
    public async Task GetUsuarios_DevuelveRolActivo()
    {
        var ctx = TestHelpers.NewContext();
        var rol = await SeedRolAsync(ctx);
        var usuario = new Usuario { Nombre = "Ana", Email = "ana@test.com", PasswordHash = "x", Estado = "activo" };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        ctx.UsuarioRoles.Add(new UsuarioRol { IdUsuario = usuario.IdUsuario, IdRol = rol.IdRol, Estado = "activo" });
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_usuarios" });
        var controller = NewController(ctx, admin);

        var result = controller.GetUsuarios();

        var ok = Assert.IsType<OkObjectResult>(result);
        var lista = (System.Collections.IEnumerable)ok.Value!;
        var primero = lista.Cast<object>().First();
        Assert.Equal("Analista QA", primero.GetType().GetProperty("Rol")!.GetValue(primero));
    }

    [Fact(DisplayName = "Update con email ya usado por otro usuario devuelve 400")]
    public async Task Update_EmailDuplicado_BadRequest()
    {
        var ctx = TestHelpers.NewContext();
        var rol = await SeedRolAsync(ctx);
        var u1 = new Usuario { Nombre = "Ana", Email = "ana@test.com", PasswordHash = "x", Estado = "activo" };
        var u2 = new Usuario { Nombre = "Beto", Email = "beto@test.com", PasswordHash = "x", Estado = "activo" };
        ctx.Usuarios.AddRange(u1, u2);
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_usuarios" });
        var controller = NewController(ctx, admin);

        var result = await controller.Update(u2.IdUsuario, new UpdateUsuarioRequest("Beto", "ana@test.com", "Analista QA"));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "Update con datos validos actualiza nombre, email y rol")]
    public async Task Update_DatosValidos_Actualiza()
    {
        var ctx = TestHelpers.NewContext();
        var rolAnalista = await SeedRolAsync(ctx, "Analista QA");
        var rolLider = await SeedRolAsync(ctx, "Líder Técnico");
        var usuario = new Usuario { Nombre = "Ana", Email = "ana@test.com", PasswordHash = "x", Estado = "activo" };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        ctx.UsuarioRoles.Add(new UsuarioRol { IdUsuario = usuario.IdUsuario, IdRol = rolAnalista.IdRol, Estado = "activo" });
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_usuarios" });
        var controller = NewController(ctx, admin);

        var result = await controller.Update(usuario.IdUsuario, new UpdateUsuarioRequest("Ana Gomez", "ana.gomez@test.com", "Líder Técnico"));

        Assert.IsType<OkObjectResult>(result);
        var actualizado = await ctx.Usuarios.FindAsync(usuario.IdUsuario);
        Assert.Equal("Ana Gomez", actualizado!.Nombre);
        Assert.Equal("ana.gomez@test.com", actualizado.Email);
        Assert.Contains(ctx.UsuarioRoles, ur => ur.IdUsuario == usuario.IdUsuario && ur.IdRol == rolLider.IdRol && ur.Estado == "activo");
    }

    [Fact(DisplayName = "Update sin permiso gestionar_usuarios devuelve 403")]
    public async Task Update_SinPermiso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var rol = await SeedRolAsync(ctx);
        var usuario = new Usuario { Nombre = "Ana", Email = "ana@test.com", PasswordHash = "x", Estado = "activo" };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();

        var soloVer = TestHelpers.BuildUser(2, new[] { "Analista QA" }, new[] { "ver_usuarios" });
        var controller = NewController(ctx, soloVer);

        var result = await controller.Update(usuario.IdUsuario, new UpdateUsuarioRequest("Ana", "ana@test.com", "Analista QA"));

        Assert.IsType<ForbidResult>(result);
    }

    [Fact(DisplayName = "ToggleEstado inactiva correctamente al usuario")]
    public async Task ToggleEstado_Inactiva()
    {
        var ctx = TestHelpers.NewContext();
        var usuario = new Usuario { Nombre = "Ana", Email = "ana@test.com", PasswordHash = "x", Estado = "activo" };
        ctx.Usuarios.Add(usuario);
        await ctx.SaveChangesAsync();
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_usuarios" });
        var controller = NewController(ctx, admin);

        var result = await controller.ToggleEstado(usuario.IdUsuario, new EstadoRequest(false));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("inactivo", (await ctx.Usuarios.FindAsync(usuario.IdUsuario))!.Estado);
    }
}
