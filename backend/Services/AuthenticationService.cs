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
        if (usuario == null || usuario.Estado != "activo")
            return null;

        if (!VerifyPassword(request.Password, usuario.PasswordHash))
            return null;

        // Migración transparente: si el hash todavía es el algoritmo legado (SHA-256),
        // se re-hashea con BCrypt en el primer login exitoso tras la migración a RNF04.
        if (!usuario.PasswordHash.StartsWith("$2"))
            usuario.PasswordHash = HashPassword(request.Password);

        // Actualizar fecha de último acceso
        usuario.FechaUltimoAcceso = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var roles = await _context.UsuarioRoles
            .Where(ur => ur.IdUsuario == usuario.IdUsuario && ur.Estado == "activo")
            .Include(ur => ur.Rol)
            .Select(ur => ur.Rol!.NombreRol)
            .ToListAsync();

        var permisos = await _context.UsuarioRoles
            .Where(ur => ur.IdUsuario == usuario.IdUsuario && ur.Estado == "activo")
            .Include(ur => ur.Rol!.RolPermisos)
            .ThenInclude(rp => rp.Permiso)
            .SelectMany(ur => ur.Rol!.RolPermisos)
            .Select(rp => rp.Permiso!.NombrePermiso)
            .Distinct()
            .ToListAsync();

        var token = GenerateJwtToken(usuario, roles, permisos);

        return new LoginResponseDto
        {
            Token = token,
            Usuario = new UsuarioDto
            {
                Id = usuario.IdUsuario,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                Activo = usuario.Estado == "activo",
                Roles = roles,
                Permisos = permisos
            }
        };
    }

    public async Task<Usuario?> GetUserByEmailAsync(string email)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public string GenerateJwtToken(Usuario usuario, List<string> roles, List<string> permisos)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "default-key-min-32-characters-here"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<System.Security.Claims.Claim>
        {
            new("sub",   usuario.IdUsuario.ToString()),
            new("email", usuario.Email),
            new("name",  usuario.Nombre)
        };

        foreach (var role in roles)
        {
            claims.Add(new(System.Security.Claims.ClaimTypes.Role, role));
            // Alias simple para que el frontend pueda leer "role" al decodificar el
            // token sin depender de la URI larga que .NET usa para ClaimTypes.Role.
            claims.Add(new("role", role));
        }

        foreach (var permiso in permisos)
            claims.Add(new("permission", permiso));

        var token = new JwtSecurityToken(
            issuer:             _configuration["Jwt:Issuer"],
            audience:           _configuration["Jwt:Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(
                                    Convert.ToInt32(_configuration["Jwt:ExpirationMinutes"] ?? "60")),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // RNF04: hash BCrypt con factor de costo 12 (≥10 exigido por la tesis).
    public string HashPassword(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool VerifyPassword(string password, string hash)
    {
        if (hash.StartsWith("$2"))
            return BCrypt.Net.BCrypt.Verify(password, hash);

        // Hash legado (SHA-256, algoritmo previo a la migración RNF04).
        // Se mantiene solo para verificar contra hashes ya persistidos;
        // HashPassword() ya no genera este formato.
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        var legacyHash = Convert.ToHexString(bytes).ToLower();
        return legacyHash == hash;
    }

    public async Task<bool> ChangePasswordAsync(int usuarioId, string currentPassword, string newPassword)
    {
        var usuario = await _context.Usuarios.FindAsync(usuarioId);
        if (usuario == null || !VerifyPassword(currentPassword, usuario.PasswordHash))
            return false;

        usuario.PasswordHash = HashPassword(newPassword);
        await _context.SaveChangesAsync();
        return true;
    }
}
