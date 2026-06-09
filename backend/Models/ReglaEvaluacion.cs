namespace DecisionSupportAPI.Models;

public class ReglaEvaluacion
{
    public int Id { get; set; }
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    public string? CondicionJson { get; set; }
    public bool Activa { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
