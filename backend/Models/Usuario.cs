using System.ComponentModel.DataAnnotations.Schema;

namespace DecisionSupportAPI.Models;

public class Usuario
{
    public int Id { get; set; }
    public required string Nombre { get; set; }
    public required string Email { get; set; }
    [Column("contrasena_hash")]
    public required string ContrasenaHash { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public ICollection<UsuarioRol> UsuariosRoles { get; set; } = [];
}
