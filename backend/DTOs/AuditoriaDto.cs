namespace DecisionSupportAPI.DTOs;

public class AuditoriaDto
{
    public int Id { get; set; }
    public int? UsuarioId { get; set; }
    public string? Usuario { get; set; }
    public string? EntidadAfectada { get; set; }
    public int? IdRegistroAfectado { get; set; }
    public string? Accion { get; set; }
    public string? Detalle { get; set; }
    public DateTime FechaEvento { get; set; }
    public string? IpOrigen { get; set; }
}
