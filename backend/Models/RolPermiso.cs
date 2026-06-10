namespace DecisionSupportAPI.Models;

public class RolPermiso
{
    public int IdRolPermiso { get; set; }
    public int IdRol { get; set; }
    public int IdPermiso { get; set; }

    public Rol? Rol { get; set; }
    public Permiso? Permiso { get; set; }
}
