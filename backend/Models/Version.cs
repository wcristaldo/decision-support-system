namespace DecisionSupportAPI.Models;

public class Version
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public required string NumeroVersion { get; set; }
    public string? Descripcion { get; set; }
    public DateTime FechaVersion { get; set; } = DateTime.UtcNow;
    public string Estado { get; set; } = "pendiente";

    public Proyecto? Proyecto { get; set; }
    public ICollection<ResultadoPrueba> ResultadosPrueba { get; set; } = [];
}
