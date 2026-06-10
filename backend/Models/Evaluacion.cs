namespace DecisionSupportAPI.Models;

public class Evaluacion
{
    public int Id { get; set; }
    public int ResultadoId { get; set; }
    public DateTime FechaEvaluacion { get; set; } = DateTime.UtcNow;
    public string EstadoEvaluacion { get; set; } = "pendiente";
    public string? ResumenEvaluacion { get; set; }

    public ResultadoPrueba? Resultado { get; set; }
    public ICollection<EvaluacionRegla> EvaluacionReglas { get; set; } = [];
    public ICollection<Recomendacion> Recomendaciones { get; set; } = [];
}
