namespace DecisionSupportAPI.Models;

public class Permiso
{
    public int Id { get; set; }
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<RolPermiso> RolesPermisos { get; set; } = [];
}
