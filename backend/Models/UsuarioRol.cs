namespace DecisionSupportAPI.Models;

public class UsuarioRol
{
    public int IdUsuarioRol { get; set; }
    public int IdUsuario { get; set; }
    public int IdRol { get; set; }
    public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;
    public string Estado { get; set; } = "activo";

    public Usuario? Usuario { get; set; }
    public Rol? Rol { get; set; }
}
