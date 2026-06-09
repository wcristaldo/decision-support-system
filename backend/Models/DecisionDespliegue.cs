namespace DecisionSupportAPI.Models;

public class DecisionDespliegue
{
    public int Id { get; set; }
    public int VersionId { get; set; }
    public int UsuarioId { get; set; }
    public string? Decision { get; set; }
    public string? Justificacion { get; set; }
    public DateTime FechaDecision { get; set; } = DateTime.UtcNow;

    public Version? Version { get; set; }
    public Usuario? Usuario { get; set; }
}
