namespace DecisionSupportAPI.Models;

public class DecisionDespliegue
{
    public int Id { get; set; }
    public int RecomendacionId { get; set; }
    public int? UsuarioDecisorId { get; set; }
    public string? DecisionFinal { get; set; }
    public string? Comentario { get; set; }
    public DateTime FechaDecision { get; set; } = DateTime.UtcNow;

    public Recomendacion? Recomendacion { get; set; }
    public Usuario? UsuarioDecisor { get; set; }
}
