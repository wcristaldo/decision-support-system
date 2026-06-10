namespace DecisionSupportAPI.DTOs;

public class MetricaDto
{
    public int Id { get; set; }
    public int ResultadoId { get; set; }
    public string? NombreMetrica { get; set; }
    public decimal? ValorMetrica { get; set; }
    public string? Unidad { get; set; }
    public DateTime FechaCalculo { get; set; }
}
