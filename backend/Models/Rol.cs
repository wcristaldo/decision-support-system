namespace DecisionSupportAPI.Models;

public class Rol
{
    public int IdRol { get; set; }
    public required string NombreRol { get; set; }
    public string? Descripcion { get; set; }
    public string Estado { get; set; } = "activo";

    public ICollection<UsuarioRol> UsuarioRoles { get; set; } = [];
    public ICollection<RolPermiso> RolPermisos { get; set; } = [];
}
