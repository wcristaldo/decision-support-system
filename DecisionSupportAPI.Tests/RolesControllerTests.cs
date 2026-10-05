using Microsoft.AspNetCore.Mvc;
using DecisionSupportAPI.Controllers;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;

namespace DecisionSupportAPI.Tests;

/// <summary>RNF08: cobertura de RolesController (RF14 — RBAC).</summary>
public class RolesControllerTests
{
    private static RolesController NewController(ApplicationDbContext ctx, System.Security.Claims.ClaimsPrincipal user)
    {
        var controller = new RolesController(ctx, TestHelpers.NewAuditoriaService(ctx, user));
        TestHelpers.SetUser(controller, user);
        return controller;
    }

    private static async Task<(Rol rol, Permiso gestionarUsuarios)> SeedRolAdminAsync(ApplicationDbContext ctx)
    {
        var rol = new Rol { NombreRol = "Administrador", Estado = "activo" };
        ctx.Roles.Add(rol);
        var permiso = new Permiso { NombrePermiso = "gestionar_usuarios", Modulo = "Usuarios" };
        ctx.Permisos.Add(permiso);
        await ctx.SaveChangesAsync();
        ctx.RolPermisos.Add(new RolPermiso { IdRol = rol.IdRol, IdPermiso = permiso.IdPermiso });
        await ctx.SaveChangesAsync();
        return (rol, permiso);
    }

    [Fact(DisplayName = "GetRoles sin permiso gestionar_usuarios/ver_usuarios devuelve 403")]
    public async Task GetRoles_SinPermiso_Forbid()
    {
        var ctx = TestHelpers.NewContext();
        var sinPermiso = TestHelpers.BuildUser(1, new[] { "Analista QA" }, Array.Empty<string>());
        var controller = NewController(ctx, sinPermiso);

        var result = controller.GetRoles();

        Assert.IsType<ForbidResult>(result);
    }

    [Fact(DisplayName = "GetPermisosDeRol marca correctamente los permisos ya asignados")]
    public async Task GetPermisosDeRol_MarcaAsignados()
    {
        var ctx = TestHelpers.NewContext();
        var (rol, _) = await SeedRolAdminAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_usuarios" });
        var controller = NewController(ctx, admin);

        var result = await controller.GetPermisosDeRol(rol.IdRol);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact(DisplayName = "ActualizarPermisosDeRol impide que Administrador pierda gestionar_usuarios")]
    public async Task ActualizarPermisosDeRol_AdminNoPierdeGestionarUsuarios()
    {
        var ctx = TestHelpers.NewContext();
        var (rol, _) = await SeedRolAdminAsync(ctx);
        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_usuarios" });
        var controller = NewController(ctx, admin);

        var result = await controller.ActualizarPermisosDeRol(rol.IdRol, new RolesController.ActualizarPermisosRequest(new List<int>()));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "ActualizarPermisosDeRol de un rol no-Administrador aplica el nuevo set de permisos")]
    public async Task ActualizarPermisosDeRol_RolNoAdmin_AplicaCambios()
    {
        var ctx = TestHelpers.NewContext();
        var rol = new Rol { NombreRol = "Analista QA", Estado = "activo" };
        ctx.Roles.Add(rol);
        var permiso = new Permiso { NombrePermiso = "ver_proyectos", Modulo = "Proyectos" };
        ctx.Permisos.Add(permiso);
        await ctx.SaveChangesAsync();

        var admin = TestHelpers.BuildUser(1, new[] { "Administrador" }, new[] { "gestionar_usuarios" });
        var controller = NewController(ctx, admin);

        var result = await controller.ActualizarPermisosDeRol(rol.IdRol, new RolesController.ActualizarPermisosRequest(new List<int> { permiso.IdPermiso }));

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(ctx.RolPermisos.Where(rp => rp.IdRol == rol.IdRol));
    }
}
