namespace DecisionSupportAPI.Models;

public class Permiso
{
    public int IdPermiso { get; set; }
    public required string NombrePermiso { get; set; }
    public string? Descripcion { get; set; }
    public string? Modulo { get; set; }

    public ICollection<RolPermiso> RolPermisos { get; set; } = [];
}
