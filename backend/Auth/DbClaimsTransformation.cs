using System.Security.Claims;
using DecisionSupportAPI.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace DecisionSupportAPI.Auth;

/// <summary>
/// Reemplaza, en cada request autenticado con JWT, los claims de rol y permiso
/// que trae el token por el estado REAL y actual del usuario en la base de
/// datos. Sin esto, revocar un permiso, cambiar un rol o desactivar una cuenta
/// solo tendría efecto en el próximo login del usuario afectado — el token ya
/// emitido seguiría otorgando el acceso viejo hasta que expire (hasta
/// Jwt:ExpirationMinutes, ver appsettings). Con esto, el cambio es inmediato.
/// </summary>
public class DbClaimsTransformation : IClaimsTransformation
{
    private readonly ApplicationDbContext _context;

    public DbClaimsTransformation(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return principal;

        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier) ?? principal.FindFirst("sub");
        if (idClaim == null || !int.TryParse(idClaim.Value, out var usuarioId))
            return principal;

        // Limpiar los claims de rol/permiso que trajo el JWT — se reconstruyen
        // desde el estado actual de la base a continuación.
        foreach (var c in principal.FindAll(ClaimTypes.Role).ToList())
            identity.RemoveClaim(c);
        foreach (var c in principal.FindAll("permission").ToList())
            identity.RemoveClaim(c);

        var usuario = await _context.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsuario == usuarioId);

        // Usuario desactivado o eliminado: se queda sin rol ni permisos, así que
        // cualquier [Authorize(Roles=...)] o política basada en "permission" falla.
        if (usuario == null || usuario.Estado != "activo")
            return principal;

        var rolesActivos = await _context.UsuarioRoles
            .Where(ur => ur.IdUsuario == usuarioId && ur.Estado == "activo")
            .Include(ur => ur.Rol)
            .Where(ur => ur.Rol!.Estado == "activo")
            .Select(ur => ur.Rol!)
            .ToListAsync();

        foreach (var rol in rolesActivos)
            identity.AddClaim(new Claim(ClaimTypes.Role, rol.NombreRol));

        var rolIds = rolesActivos.Select(r => r.IdRol).ToList();
        var permisos = await _context.RolPermisos
            .Where(rp => rolIds.Contains(rp.IdRol))
            .Select(rp => rp.Permiso!.NombrePermiso)
            .Distinct()
            .ToListAsync();

        foreach (var permiso in permisos)
            identity.AddClaim(new Claim("permission", permiso));

        return principal;
    }
}
