namespace DecisionSupportAPI.Models;

// RF13: asociación usuario↔proyecto. Administrador no necesita fila acá —
// ve todos los proyectos sin restricción (ver DbClaimsTransformation.cs).
public class UsuarioProyecto
{
    public int IdUsuarioProyecto { get; set; }
    public int IdUsuario { get; set; }
    public int IdProyecto { get; set; }
    public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;

    public Usuario? Usuario { get; set; }
    public Proyecto? Proyecto { get; set; }
}
