namespace DecisionSupportAPI.Models;

public class Version
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public required string NumeroVersion { get; set; }
    public string? Descripcion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Proyecto? Proyecto { get; set; }
    public ICollection<ResultadoPrueba> ResultadosPrueba { get; set; } = [];
    public ICollection<Metrica> Metricas { get; set; } = [];
    public ICollection<Evaluacion> Evaluaciones { get; set; } = [];
    public ICollection<Recomendacion> Recomendaciones { get; set; } = [];
    public ICollection<DecisionDespliegue> DecisionesDespliegue { get; set; } = [];
}
