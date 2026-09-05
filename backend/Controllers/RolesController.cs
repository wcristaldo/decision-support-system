using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

/// <summary>
/// RF14: gestión de roles y permisos del sistema (RBAC). Los permisos se
/// asignan a ROLES, no a usuarios individuales — principio de Sandhu et al.
/// (1996) ya citado en la tesis. Un usuario obtiene sus permisos exclusivamente
/// a través del rol que tiene asignado (ver UsuariosController).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoriaService;

    public RolesController(ApplicationDbContext context, IAuditoriaService auditoriaService)
    {
        _context = context;
        _auditoriaService = auditoriaService;
    }

    private bool IsAdmin() => User.IsInRole("Administrador");

    // GET /api/roles — devuelve todos los roles activos
    [HttpGet]
    public IActionResult GetRoles()
    {
        var roles = _context.Roles
            .Where(r => r.Estado == "activo")
            .OrderBy(r => r.NombreRol)
            .Select(r => new { r.IdRol, r.NombreRol })
            .ToList();

        return Ok(roles);
    }

    // GET /api/roles/{id}/permisos — matriz: todos los permisos + cuáles tiene este rol.
    // Solo Administrador: es información de configuración de seguridad, no de uso general.
    [HttpGet("{id}/permisos")]
    public async Task<IActionResult> GetPermisosDeRol(int id)
    {
        if (!IsAdmin()) return Forbid();

        var rol = await _context.Roles.FindAsync(id);
        if (rol == null) return NotFound(new { message = "Rol no encontrado." });

        var asignados = await _context.RolPermisos
            .Where(rp => rp.IdRol == id)
            .Select(rp => rp.IdPermiso)
            .ToListAsync();

        var permisos = await _context.Permisos
            .OrderBy(p => p.Modulo).ThenBy(p => p.NombrePermiso)
            .Select(p => new
            {
                p.IdPermiso,
                p.NombrePermiso,
                p.Descripcion,
                p.Modulo,
                Asignado = asignados.Contains(p.IdPermiso),
            })
            .ToListAsync();

        return Ok(new { rol.IdRol, rol.NombreRol, permisos });
    }

    public record ActualizarPermisosRequest(List<int> PermisoIds);

    // PUT /api/roles/{id}/permisos — reemplaza el set completo de permisos del rol.
    [HttpPut("{id}/permisos")]
    public async Task<IActionResult> ActualizarPermisosDeRol(int id, [FromBody] ActualizarPermisosRequest request)
    {
        if (!IsAdmin()) return Forbid();

        var rol = await _context.Roles.FindAsync(id);
        if (rol == null) return NotFound(new { message = "Rol no encontrado." });

        // Salvaguarda: Administrador siempre debe conservar el permiso de gestionar
        // usuarios; si no, un admin distraído podría dejar el sistema sin nadie que
        // pueda revertir el cambio.
        if (rol.NombreRol == "Administrador")
        {
            var permisoGestionUsuarios = await _context.Permisos
                .Where(p => p.NombrePermiso == "gestionar_usuarios")
                .Select(p => p.IdPermiso)
                .FirstOrDefaultAsync();

            if (permisoGestionUsuarios != 0 && !request.PermisoIds.Contains(permisoGestionUsuarios))
                return BadRequest(new { message = "El rol Administrador no puede perder el permiso 'gestionar_usuarios'." });
        }

        var actuales = await _context.RolPermisos.Where(rp => rp.IdRol == id).ToListAsync();
        _context.RolPermisos.RemoveRange(actuales);

        var permisosValidos = await _context.Permisos
            .Where(p => request.PermisoIds.Contains(p.IdPermiso))
            .Select(p => p.IdPermiso)
            .ToListAsync();

        foreach (var permisoId in permisosValidos)
            _context.RolPermisos.Add(new RolPermiso { IdRol = id, IdPermiso = permisoId });

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Update", "RolPermiso", id,
            $"Permisos actualizados para el rol {rol.NombreRol}: {permisosValidos.Count} permiso(s) asignado(s)");

        return Ok(new
        {
            message = "Permisos actualizados. El cambio ya está activo para todos los usuarios de este rol, incluso con la sesión abierta.",
        });
    }
}
