namespace DecisionSupportAPI.Models;

public class EvaluacionRegla
{
    public int Id { get; set; }
    public int EvaluacionId { get; set; }
    public int ReglaId { get; set; }
    public string? ResultadoRegla { get; set; }
    public string? Observacion { get; set; }

    public Evaluacion? Evaluacion { get; set; }
    public ReglaEvaluacion? Regla { get; set; }
}
