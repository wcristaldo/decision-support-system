namespace DecisionSupportAPI.Models;

public class ResultadoPrueba
{
    public int Id { get; set; }
    public int VersionId { get; set; }
    public required string NombrePrueba { get; set; }
    public string? TipoPrueba { get; set; }
    public string? Estado { get; set; }
    public decimal? TiempoEjecucion { get; set; }
    public string? Mensaje { get; set; }
    public DateTime FechaEjecucion { get; set; } = DateTime.UtcNow;

    public Version? Version { get; set; }
}
