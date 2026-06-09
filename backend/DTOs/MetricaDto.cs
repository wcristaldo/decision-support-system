namespace DecisionSupportAPI.DTOs;

public class MetricaDto
{
    public int Id { get; set; }
    public int VersionId { get; set; }
    public string? NombreMetrica { get; set; }
    public decimal? Valor { get; set; }
    public string? Unidad { get; set; }
    public DateTime FechaCalculo { get; set; }
}
