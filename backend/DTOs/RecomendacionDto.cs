namespace DecisionSupportAPI.DTOs;

public class RecomendacionDto
{
    public int Id { get; set; }
    public int EvaluacionId { get; set; }
    public string? TipoRecomendacion { get; set; }
    public string? Justificacion { get; set; }
    public DateTime FechaGeneracion { get; set; }
}

/// <summary>
/// Veredicto real (cumple/revisar/no_cumple) que el motor de recomendación calculó
/// para un criterio de evaluación puntual. Permite que el frontend coloree cada
/// métrica según el umbral realmente configurado, en vez de una escala fija propia.
/// </summary>
public class EvaluacionReglaDto
{
    public string? Criterio { get; set; }
    public string? ResultadoRegla { get; set; }
    public string? Observacion { get; set; }
}
