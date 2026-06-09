using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DecisionSupportAPI.Services;

public interface IAuthenticationService
{
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request);
    Task<Usuario?> GetUserByEmailAsync(string email);
    Task<bool> ChangePasswordAsync(int usuarioId, string currentPassword, string newPassword);
    string GenerateJwtToken(Usuario usuario, List<string> roles, List<string> permisos);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthenticationService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request)
    {
        var usuario = await GetUserByEmailAsync(request.Email);
        if (usuario == null || !VerifyPassword(request.Password, usuario.ContrasenaHash))
            return null;

        var roles = await _context.UsuariosRoles
            .Where(ur => ur.UsuarioId == usuario.Id)
            .Include(ur => ur.Rol)
            .Select(ur => ur.Rol!.Nombre)
            .ToListAsync();

        var permisos = await _context.UsuariosRoles
            .Where(ur => ur.UsuarioId == usuario.Id)
            .Include(ur => ur.Rol!.RolesPermisos)
            .SelectMany(ur => ur.Rol!.RolesPermisos)
            .Include(rp => rp.Permiso)
            .Select(rp => rp.Permiso!.Nombre)
            .Distinct()
            .ToListAsync();

        var token = GenerateJwtToken(usuario, roles, permisos);

        return new LoginResponseDto
        {
            Token = token,
            Usuario = new UsuarioDto
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                Activo = usuario.Activo,
                Roles = roles,
                Permisos = permisos
            }
        };
    }

    public async Task<Usuario?> GetUserByEmailAsync(string email)
    {
        return await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == email);
    }

    public string GenerateJwtToken(Usuario usuario, List<string> roles, List<string> permisos)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "default-key"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim("sub", usuario.Id.ToString()),
            new System.Security.Claims.Claim("email", usuario.Email),
            new System.Security.Claims.Claim("name", usuario.Nombre)
        };

        foreach (var role in roles)
        {
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
        }

        foreach (var permiso in permisos)
        {
            claims.Add(new System.Security.Claims.Claim("permission", permiso));
        }

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(Convert.ToInt32(_configuration["Jwt:ExpirationMinutes"] ?? "60")),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(hashedBytes).ToLower();
    }

    public bool VerifyPassword(string password, string hash)
    {
        var hashOfInput = HashPassword(password);
        return hashOfInput == hash;
    }

    public async Task<bool> ChangePasswordAsync(int usuarioId, string currentPassword, string newPassword)
    {
        var usuario = await _context.Usuarios.FindAsync(usuarioId);
        if (usuario == null || !VerifyPassword(currentPassword, usuario.ContrasenaHash))
            return false;

        usuario.ContrasenaHash = HashPassword(newPassword);
        usuario.FechaActualizacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }
}
