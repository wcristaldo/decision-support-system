namespace DecisionSupportAPI.Models;

public class Recomendacion
{
    public int Id { get; set; }
    public int EvaluacionId { get; set; }
    public string? TipoRecomendacion { get; set; }
    public string? Justificacion { get; set; }
    public DateTime FechaGeneracion { get; set; } = DateTime.UtcNow;

    public Evaluacion? Evaluacion { get; set; }
    public ICollection<DecisionDespliegue> DecisionesDespliegue { get; set; } = [];
}
