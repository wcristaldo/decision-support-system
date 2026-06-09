namespace DecisionSupportAPI.Models;

public class Evaluacion
{
    public int Id { get; set; }
    public int VersionId { get; set; }
    public decimal? PuntuacionCalidad { get; set; }
    public decimal? PuntuacionRiesgo { get; set; }
    public string? Observaciones { get; set; }
    public DateTime FechaEvaluacion { get; set; } = DateTime.UtcNow;

    public Version? Version { get; set; }
}
