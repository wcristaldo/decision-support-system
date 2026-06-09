namespace DecisionSupportAPI.Models;

public class Rol
{
    public int Id { get; set; }
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<UsuarioRol> UsuariosRoles { get; set; } = [];
    public ICollection<RolPermiso> RolesPermisos { get; set; } = [];
}
