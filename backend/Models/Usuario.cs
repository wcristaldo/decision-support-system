namespace DecisionSupportAPI.Models;

public class Usuario
{
    public int IdUsuario { get; set; }
    public required string Nombre { get; set; }
    public string? Apellido { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string Estado { get; set; } = "activo";
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaUltimoAcceso { get; set; }

    public ICollection<UsuarioRol> UsuarioRoles { get; set; } = [];
}
