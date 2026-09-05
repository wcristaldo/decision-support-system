using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Services;
using DecisionSupportAPI.Data;
using System.Security.Claims;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ApplicationDbContext _context;
    private readonly IAuditoriaService _auditoriaService;
    private readonly IHostEnvironment _env;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthenticationService authService, ApplicationDbContext context, IAuditoriaService auditoriaService, IHostEnvironment env, ILogger<AuthController> logger)
    {
        _authService = authService;
        _context = context;
        _auditoriaService = auditoriaService;
        _env = env;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.LoginAsync(request);
        if (result == null)
            return Unauthorized(new { message = "Credenciales inválidas" });

        var usuario = _context.Usuarios.FirstOrDefault(u => u.Email == request.Email);
        if (usuario != null)
        {
            _ = _auditoriaService.RegistrarAsync("Login", "Usuario", usuario.IdUsuario, $"Login exitoso: {request.Email}");
        }

        return Ok(result);
    }

    /// <summary>
    /// GET /api/auth/me — datos del usuario autenticado, con rol y permisos
    /// LEÍDOS EN VIVO (los mismos claims que DbClaimsTransformation reconstruye
    /// desde la base en cada request), no los que trae el JWT original. Así,
    /// la pantalla "Mi perfil" siempre coincide con lo que el backend realmente
    /// va a autorizar, aunque un permiso se haya revocado recién.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            nombre   = User.FindFirst("name")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "",
            email    = User.FindFirst("email")?.Value ?? "",
            roles    = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
            permisos = User.FindAll("permission").Select(c => c.Value).ToList(),
        });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.NewPassword != request.ConfirmPassword)
            return BadRequest(new { message = "Las nuevas contraseñas no coinciden" });

        var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                          ?? User.FindFirst("sub");
        if (usuarioIdClaim == null || !int.TryParse(usuarioIdClaim.Value, out var usuarioId))
            return Unauthorized(new { message = "Usuario no identificado" });

        var result = await _authService.ChangePasswordAsync(usuarioId, request.CurrentPassword, request.NewPassword);
        if (!result)
            return BadRequest(new { message = "Contraseña actual incorrecta" });

        await _auditoriaService.RegistrarAsync("Password Change", "Usuario", usuarioId, $"Cambio de contraseña");

        return Ok(new { message = "Contraseña actualizada correctamente" });
    }

    [Authorize]
    [HttpPost("reset-password/{usuarioId}")]
    public async Task<IActionResult> ResetPassword(int usuarioId, [FromBody] ResetPasswordDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!User.IsInRole("Administrador"))
            return StatusCode(403, new { message = "No eres administrador" });

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { message = "Contraseña vacía" });

        try
        {
            var user = _context.Usuarios.FirstOrDefault(u => u.IdUsuario == usuarioId);
            if (user == null)
                return NotFound(new { message = "Usuario no encontrado" });

            user.PasswordHash = _authService.HashPassword(request.NewPassword);
            _context.SaveChanges();

            await _auditoriaService.RegistrarAsync("Password Change", "Usuario", usuarioId, $"Reset de contraseña por administrador");

            return Ok(new { message = "Éxito" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "reset-password falló para usuarioId {UsuarioId}", usuarioId);
            if (_env.IsDevelopment())
                return StatusCode(500, new { error = ex.Message });
            return StatusCode(500, new { error = "No se pudo actualizar la contraseña." });
        }
    }
}
