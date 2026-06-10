namespace DecisionSupportAPI.Models;

public class Auditoria
{
    public int Id { get; set; }
    public int? UsuarioId { get; set; }
    public string? EntidadAfectada { get; set; }
    public string? IdRegistroAfectado { get; set; }
    public string? Accion { get; set; }
    public string? Detalle { get; set; }
    public DateTime FechaEvento { get; set; } = DateTime.UtcNow;
    public string? IpOrigen { get; set; }

    public Usuario? Usuario { get; set; }
}
