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

    // Según RF10: gestión de usuarios es exclusiva del rol Administrador
    private bool IsAdmin() => User.IsInRole("Administrador");

    // ── GET /api/usuarios ─────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult GetUsuarios()
    {
        if (!IsAdmin()) return Forbid();

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
        if (!IsAdmin()) return Forbid();

        // ── Verificar límite de plan ──────────────────────────────────────
        var limite = await _suscripcionService.VerificarLimiteUsuariosAsync();
        if (!limite.Permitido)
            return StatusCode(402, new { message = limite.Mensaje, codigo = "LIMITE_USUARIOS" });

        if (string.IsNullOrWhiteSpace(request.Nombre) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Rol))
            return BadRequest(new { message = "Todos los campos son obligatorios." });

        if (_context.Usuarios.Any(u => u.Email == request.Email.Trim()))
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
        if (!IsAdmin()) return Forbid();

        if (string.IsNullOrWhiteSpace(request.Nombre) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Rol))
            return BadRequest(new { message = "Nombre, correo y rol son obligatorios." });

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

        // Desactivar roles actuales y asignar el nuevo
        var rolesActivos = _context.UsuarioRoles
            .Where(ur => ur.IdUsuario == id && ur.Estado == "activo")
            .ToList();

        foreach (var ur in rolesActivos)
            ur.Estado = "inactivo";

        _context.UsuarioRoles.Add(new UsuarioRol
        {
            IdUsuario       = id,
            IdRol           = rol.IdRol,
            Estado          = "activo",
            FechaAsignacion = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync();
        await _auditoriaService.RegistrarAsync("Update", "Usuario", id, $"Usuario actualizado: {usuario.Nombre} - Nuevo rol: {request.Rol}");

        return Ok(new { message = "Usuario actualizado correctamente." });
    }

    // ── PATCH /api/usuarios/{id}/estado ──────────────────────────────────────
    [HttpPatch("{id}/estado")]
    public async Task<IActionResult> ToggleEstado(int id, [FromBody] EstadoRequest request)
    {
        if (!IsAdmin()) return Forbid();

        var usuario = _context.Usuarios.FirstOrDefault(u => u.IdUsuario == id);
        if (usuario == null) return NotFound(new { message = "Usuario no encontrado." });

        usuario.Estado = request.Activo ? "activo" : "inactivo";
        _context.Usuarios.Update(usuario);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Update", "Usuario", id, $"Usuario {(request.Activo ? "activado" : "inactivado")}: {usuario.Nombre}");

        return Ok(new { message = $"Usuario {(request.Activo ? "activado" : "inactivado")} correctamente." });
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreateUsuarioRequest(string Nombre, string Email, string Password, string Rol);
public record UpdateUsuarioRequest(string Nombre, string Email, string Rol);
public record EstadoRequest(bool Activo);
