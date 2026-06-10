namespace DecisionSupportAPI.Models;

public class Metrica
{
    public int Id { get; set; }
    public int ResultadoId { get; set; }
    public required string NombreMetrica { get; set; }
    public decimal? ValorMetrica { get; set; }
    public string? Unidad { get; set; }
    public DateTime FechaCalculo { get; set; } = DateTime.UtcNow;

    public ResultadoPrueba? Resultado { get; set; }
}
