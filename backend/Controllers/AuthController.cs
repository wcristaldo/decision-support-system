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

    public AuthController(IAuthenticationService authService, ApplicationDbContext context, IAuditoriaService auditoriaService)
    {
        _authService = authService;
        _context = context;
        _auditoriaService = auditoriaService;
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

    [HttpPost("debug/hash")]
    public IActionResult GetHash([FromBody] LoginRequestDto request)
    {
        var hash = _authService.HashPassword(request.Password);
        return Ok(new { password = request.Password, hash });
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
    public async Task<IActionResult> ResetPassword(int usuarioId, [FromBody] Dictionary<string, string> request)
    {
        try
        {
            if (!User.IsInRole("ADMIN") && !User.IsInRole("Administrador"))
                return Forbid("No eres administrador");

            if (!request.ContainsKey("newPassword"))
                return BadRequest(new { message = "newPassword requerido" });

            var pwd = request["newPassword"];
            if (string.IsNullOrWhiteSpace(pwd))
                return BadRequest(new { message = "Contraseña vacía" });

            var user = _context.Usuarios.FirstOrDefault(u => u.IdUsuario == usuarioId);
            if (user == null)
                return NotFound(new { message = "Usuario no encontrado" });

            user.PasswordHash = _authService.HashPassword(pwd);
            _context.SaveChanges();

            await _auditoriaService.RegistrarAsync("Password Change", "Usuario", usuarioId, $"Reset de contraseña por administrador");

            return Ok(new { message = "Éxito" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
