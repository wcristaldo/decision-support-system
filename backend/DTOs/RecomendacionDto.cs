namespace DecisionSupportAPI.DTOs;

public class RecomendacionDto
{
    public int Id { get; set; }
    public int EvaluacionId { get; set; }
    public string? TipoRecomendacion { get; set; }
    public string? Justificacion { get; set; }
    public DateTime FechaGeneracion { get; set; }
}
