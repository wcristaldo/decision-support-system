namespace DecisionSupportAPI.Models;

public class Auditoria
{
    public int Id { get; set; }
    public int? UsuarioId { get; set; }
    public string? Entidad { get; set; }
    public string? Accion { get; set; }
    public string? DatosAnteriores { get; set; }
    public string? DatosNuevos { get; set; }
    public DateTime FechaEvento { get; set; } = DateTime.UtcNow;

    public Usuario? Usuario { get; set; }
}
