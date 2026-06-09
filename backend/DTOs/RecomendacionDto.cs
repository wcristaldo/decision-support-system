namespace DecisionSupportAPI.DTOs;

public class RecomendacionDto
{
    public int Id { get; set; }
    public int VersionId { get; set; }
    public string? TipoRecomendacion { get; set; }
    public string? Descripcion { get; set; }
    public decimal? Confianza { get; set; }
    public DateTime FechaGeneracion { get; set; }
}
