namespace DecisionSupportAPI.DTOs;

public class DecisionDespliegueDto
{
    public int Id { get; set; }
    public int RecomendacionId { get; set; }
    public int? UsuarioDecisorId { get; set; }
    public string? DecisionFinal { get; set; }
    public string? Comentario { get; set; }
    public DateTime FechaDecision { get; set; }
}

public class CreateDecisionDespliegueDto
{
    public int RecomendacionId { get; set; }
    public required string DecisionFinal { get; set; }
    public string? Comentario { get; set; }
}
