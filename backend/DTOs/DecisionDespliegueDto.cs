namespace DecisionSupportAPI.DTOs;

public class DecisionDespliegueDto
{
    public int Id { get; set; }
    public int VersionId { get; set; }
    public int UsuarioId { get; set; }
    public string? Decision { get; set; }
    public string? Justificacion { get; set; }
    public DateTime FechaDecision { get; set; }
}

public class CreateDecisionDespliegueDto
{
    public int VersionId { get; set; }
    public required string Decision { get; set; }
    public string? Justificacion { get; set; }
}
