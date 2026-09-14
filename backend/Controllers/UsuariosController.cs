using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAuthenticationService _authService;
    private readonly IAuditoriaService _auditoriaService;
    private readonly ISuscripcionService _suscripcionService;

    public UsuariosController(ApplicationDbContext context, IAuthenticationService authService, IAuditoriaService auditoriaService, ISuscripcionService suscripcionService)
    {
        _context = context;
        _authService = authService;
        _auditoriaService = auditoriaService;
        _suscripcionService = suscripcionService;
    }

    // RBAC (RF14): se evalúa el permiso real del claim "permission", no el
    // nombre del rol — así, un rol distinto de "Administrador" al que se le
    // otorgue gestionar_usuarios/ver_usuarios obtiene acceso de inmediato,
    // sin depender de que ese rol se llame literalmente "Administrador".
    private bool TienePermiso(string permiso) => User.HasClaim("permission", permiso);
    private bool PuedeGestionar() => TienePermiso("gestionar_usuarios");
    private bool PuedeVer() => TienePermiso("ver_usuarios") || PuedeGestionar();

    private static bool EsEmailValido(string email)
    {
        try { return new System.Net.Mail.MailAddress(email.Trim()).Address == email.Trim(); }
        catch (FormatException) { return false; }
    }

    // ── GET /api/usuarios ─────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult GetUsuarios()
    {
        if (!PuedeVer()) return Forbid();

        var usuarios = _context.Usuarios
            .Include(u => u.UsuarioRoles)
                .ThenInclude(ur => ur.Rol)
            .AsNoTracking()
            .ToList()
            .Select(u => new
            {
                Id = u.IdUsuario,
                u.Nombre,
                u.Apellido,
                u.Email,
                Activo = u.Estado == "activo",
                u.Estado,
                u.FechaCreacion,
                Rol = u.UsuarioRoles
                    .Where(ur => ur.Estado == "activo" && ur.Rol != null)
                    .Select(ur => ur.Rol!.NombreRol)
                    .FirstOrDefault() ?? "—",
            })
            .ToList();

        return Ok(usuarios);
    }

    // ── POST /api/usuarios ────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUsuarioRequest request)
    {
        if (!PuedeGestionar()) return Forbid();

        // ── Verificar límite de plan ──────────────────────────────────────
        var limite = await _suscripcionService.VerificarLimiteUsuariosAsync();
        if (!limite.Permitido)
            return StatusCode(402, new { message = limite.Mensaje, codigo = "LIMITE_USUARIOS" });

        if (string.IsNullOrWhiteSpace(request.Nombre) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Rol))
            return BadRequest(new { message = "Todos los campos son obligatorios." });

        if (!EsEmailValido(request.Email))
            return BadRequest(new { message = "El correo electrónico no tiene un formato válido." });

        if (request.Password.Length < 8)
            return BadRequest(new { message = "La contraseña debe tener al menos 8 caracteres." });

        // Comparar en minúsculas: el email se guarda normalizado a minúsculas
        // más abajo, así que comparar el valor crudo del request (sin normalizar)
        // dejaba pasar duplicados que solo difieren en mayúsculas/minúsculas.
        if (_context.Usuarios.Any(u => u.Email == request.Email.Trim().ToLower()))
            return BadRequest(new { message = "Ya existe un usuario con ese correo electrónico." });

        var rol = _context.Roles.FirstOrDefault(r => r.NombreRol == request.Rol);
        if (rol == null)
            return BadRequest(new { message = $"El rol '{request.Rol}' no existe en el sistema." });

        var usuario = new Usuario
        {
            Nombre       = request.Nombre.Trim(),
            Email        = request.Email.Trim().ToLower(),
            PasswordHash = _authService.HashPassword(request.Password),
            Estado       = "activo",
            FechaCreacion = DateTime.UtcNow,
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        _context.UsuarioRoles.Add(new UsuarioRol
        {
            IdUsuario        = usuario.IdUsuario,
            IdRol            = rol.IdRol,
            Estado           = "activo",
            FechaAsignacion  = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Create", "Usuario", usuario.IdUsuario, $"Usuario creado: {usuario.Nombre} ({usuario.Email}) - Rol: {request.Rol}");

        return Ok(new { id = usuario.IdUsuario, message = "Usuario creado correctamente." });
    }

    // ── PUT /api/usuarios/{id} ────────────────────────────────────────────────
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUsuarioRequest request)
    {
        if (!PuedeGestionar()) return Forbid();

        if (string.IsNullOrWhiteSpace(request.Nombre) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Rol))
            return BadRequest(new { message = "Nombre, correo y rol son obligatorios." });

        if (!EsEmailValido(request.Email))
            return BadRequest(new { message = "El correo electrónico no tiene un formato válido." });

        var usuario = _context.Usuarios.FirstOrDefault(u => u.IdUsuario == id);
        if (usuario == null) return NotFound(new { message = "Usuario no encontrado." });

        if (_context.Usuarios.Any(u => u.Email == request.Email.Trim().ToLower() && u.IdUsuario != id))
            return BadRequest(new { message = "Ya existe otro usuario con ese correo electrónico." });

        var rol = _context.Roles.FirstOrDefault(r => r.NombreRol == request.Rol);
        if (rol == null)
            return BadRequest(new { message = $"El rol '{request.Rol}' no existe en el sistema." });

        // Actualizar datos básicos
        usuario.Nombre = request.Nombre.Trim();
        usuario.Email  = request.Email.Trim().ToLower();
        _context.Usuarios.Update(usuario);

        // Desactivar roles actuales y asignar el nuevo (solo si realmente cambió:
        // insertar una fila nueva para el mismo id_rol que ya tiene activo viola
        // la restricción de unicidad usuario_rol_id_usuario_id_rol_key, porque la
        // fila anterior con ese mismo par (id_usuario, id_rol) sigue existiendo,
        // solo que inactiva).
        var rolesActivos = _context.UsuarioRoles
            .Where(ur => ur.IdUsuario == id && ur.Estado == "activo")
            .ToList();

        if (!rolesActivos.Any(ur => ur.IdRol == rol.IdRol))
        {
            foreach (var ur in rolesActivos)
                ur.Estado = "inactivo";

            _context.UsuarioRoles.Add(new UsuarioRol
            {
                IdUsuario       = id,
                IdRol           = rol.IdRol,
                Estado          = "activo",
                FechaAsignacion = DateTime.UtcNow,
            });
        }

        await _context.SaveChangesAsync();
        await _auditoriaService.RegistrarAsync("Update", "Usuario", id, $"Usuario actualizado: {usuario.Nombre} - Nuevo rol: {request.Rol}");

        return Ok(new { message = "Usuario actualizado correctamente." });
    }

    // ── PATCH /api/usuarios/{id}/estado ──────────────────────────────────────
    [HttpPatch("{id}/estado")]
    public async Task<IActionResult> ToggleEstado(int id, [FromBody] EstadoRequest request)
    {
        if (!PuedeGestionar()) return Forbid();

        var usuario = _context.Usuarios.FirstOrDefault(u => u.IdUsuario == id);
        if (usuario == null) return NotFound(new { message = "Usuario no encontrado." });

        usuario.Estado = request.Activo ? "activo" : "inactivo";
        _context.Usuarios.Update(usuario);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Update", "Usuario", id, $"Usuario {(request.Activo ? "activado" : "inactivado")}: {usuario.Nombre}");

        return Ok(new { message = $"Usuario {(request.Activo ? "activado" : "inactivado")} correctamente." });
    }

    // ── GET /api/usuarios/{id}/proyectos ──────────────────────────────────────
    // RF13: proyectos asignados a este usuario. Administrador no tiene fila en
    // usuario_proyecto (ve todo sin restricción), así que devuelve una lista
    // vacía con isAdmin=true para que el frontend lo muestre correctamente.
    [HttpGet("{id}/proyectos")]
    public async Task<IActionResult> GetProyectosDeUsuario(int id)
    {
        if (!PuedeVer()) return Forbid();

        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles).ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u => u.IdUsuario == id);
        if (usuario == null) return NotFound(new { message = "Usuario no encontrado." });

        var esAdmin = usuario.UsuarioRoles.Any(ur => ur.Estado == "activo" && ur.Rol?.NombreRol == "Administrador");

        var proyectoIds = await _context.UsuarioProyectos
            .Where(up => up.IdUsuario == id)
            .Select(up => up.IdProyecto)
            .ToListAsync();

        return Ok(new { isAdmin = esAdmin, proyectoIds });
    }

    public record ActualizarProyectosRequest(List<int> ProyectoIds);

    // ── PUT /api/usuarios/{id}/proyectos ──────────────────────────────────────
    // Reemplaza el set completo de proyectos asignados al usuario.
    [HttpPut("{id}/proyectos")]
    public async Task<IActionResult> ActualizarProyectosDeUsuario(int id, [FromBody] ActualizarProyectosRequest request)
    {
        if (!PuedeGestionar()) return Forbid();

        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles).ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u => u.IdUsuario == id);
        if (usuario == null) return NotFound(new { message = "Usuario no encontrado." });

        var esAdmin = usuario.UsuarioRoles.Any(ur => ur.Estado == "activo" && ur.Rol?.NombreRol == "Administrador");
        if (esAdmin)
            return BadRequest(new { message = "Administrador ve todos los proyectos sin necesidad de asignación explícita." });

        var actuales = await _context.UsuarioProyectos.Where(up => up.IdUsuario == id).ToListAsync();
        _context.UsuarioProyectos.RemoveRange(actuales);

        var proyectosValidos = await _context.Proyectos
            .Where(p => request.ProyectoIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync();

        foreach (var proyectoId in proyectosValidos)
            _context.UsuarioProyectos.Add(new UsuarioProyecto { IdUsuario = id, IdProyecto = proyectoId });

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Update", "UsuarioProyecto", id,
            $"Proyectos asignados actualizados para {usuario.Nombre}: {proyectosValidos.Count} proyecto(s)");

        return Ok(new { message = "Proyectos asignados actualizados correctamente." });
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreateUsuarioRequest(string Nombre, string Email, string Password, string Rol);
public record UpdateUsuarioRequest(string Nombre, string Email, string Rol);
public record EstadoRequest(bool Activo);
