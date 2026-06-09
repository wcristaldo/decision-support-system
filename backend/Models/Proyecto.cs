namespace DecisionSupportAPI.Models;

public class Proyecto
{
    public int Id { get; set; }
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    public string Estado { get; set; } = "activo";
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public ICollection<Version> Versiones { get; set; } = [];
}
