namespace DecisionSupportAPI.Models;

public class Metrica
{
    public int Id { get; set; }
    public int VersionId { get; set; }
    public required string NombreMetrica { get; set; }
    public decimal? Valor { get; set; }
    public string? Unidad { get; set; }
    public DateTime FechaCalculo { get; set; } = DateTime.UtcNow;

    public Version? Version { get; set; }
}
