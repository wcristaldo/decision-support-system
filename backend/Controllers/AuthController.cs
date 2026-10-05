using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
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
    private readonly IEmailService _emailService;
    private readonly IHostEnvironment _env;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthenticationService authService, ApplicationDbContext context, IAuditoriaService auditoriaService, IEmailService emailService, IHostEnvironment env, ILogger<AuthController> logger)
    {
        _authService = authService;
        _context = context;
        _auditoriaService = auditoriaService;
        _emailService = emailService;
        _env = env;
        _logger = logger;
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
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
            // Debe esperarse: al ser "fire-and-forget" (sin await), ASP.NET Core
            // podía disponer el DbContext (scoped) al terminar la request mientras
            // esta tarea todavía lo usaba en segundo plano — ObjectDisposedException
            // en el mejor caso, y en la práctica dejaba la conexión pooled de Npgsql
            // en un estado de protocolo corrupto que rompía la SIGUIENTE request no
            // relacionada que reutilizara esa misma conexión del pool.
            await _auditoriaService.RegistrarAsync("Login", "Usuario", usuario.IdUsuario, $"Login exitoso: {request.Email}", usuarioIdExplicito: usuario.IdUsuario);
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

    /// <summary>
    /// POST /api/auth/forgot-password — autoservicio: genera un código de
    /// recuperación de 6 dígitos y lo envía por correo si el email existe.
    /// Siempre responde el mismo mensaje genérico, exista o no la cuenta,
    /// para no revelar qué correos están registrados (enumeración de usuarios).
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var mensajeGenerico = new { message = "Si el correo está registrado, te enviamos un código de recuperación." };

        var usuario = await _authService.GetUserByEmailAsync(request.Email);
        if (usuario == null || usuario.Estado != "activo")
            return Ok(mensajeGenerico);

        var codigo = await _authService.GenerarCodigoRecuperacionAsync(usuario.IdUsuario);
        if (codigo == null) return Ok(mensajeGenerico);

        try
        {
            await _emailService.EnviarCodigoRecuperacionAsync(usuario.Email, usuario.Nombre, codigo, 15);
        }
        catch (Exception ex)
        {
            // No se filtra al usuario si el envío falló (mismo motivo que el
            // mensaje genérico) — solo se registra para diagnóstico del admin.
            _logger.LogError(ex, "No se pudo enviar el correo de recuperación a {Email}", usuario.Email);
        }

        return Ok(mensajeGenerico);
    }

    /// <summary>
    /// POST /api/auth/reset-password-with-code — segundo paso del autoservicio:
    /// valida el código de 6 dígitos (vigente, sin usar) y fija la nueva contraseña.
    /// </summary>
    [HttpPost("reset-password-with-code")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPasswordWithCode([FromBody] ResetPasswordConCodigoDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var ok = await _authService.ResetearPasswordConCodigoAsync(request.Email, request.Codigo, request.NewPassword);
        if (!ok)
            return BadRequest(new { message = "El código es inválido o ya venció. Pedí uno nuevo." });

        var usuario = await _authService.GetUserByEmailAsync(request.Email);
        if (usuario != null)
            await _auditoriaService.RegistrarAsync("Password Change", "Usuario", usuario.IdUsuario, "Restablecimiento de contraseña por código de recuperación", usuarioIdExplicito: usuario.IdUsuario);

        return Ok(new { message = "Contraseña actualizada correctamente. Ya podés iniciar sesión." });
    }

    [Authorize]
    [HttpPost("reset-password/{usuarioId}")]
    public async Task<IActionResult> ResetPassword(int usuarioId, [FromBody] ResetPasswordDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // RBAC (RF14): resetear la contraseña de otro usuario es parte de la
        // gestión de usuarios — se evalúa el permiso real, no el nombre del rol.
        if (!User.HasClaim("permission", "gestionar_usuarios"))
            return StatusCode(403, new { message = "No tenés permiso para gestionar usuarios." });

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
