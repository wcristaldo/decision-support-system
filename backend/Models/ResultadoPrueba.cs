namespace DecisionSupportAPI.Models;

public class ResultadoPrueba
{
    public int Id { get; set; }
    public int VersionId { get; set; }
    public int? UsuarioCargaId { get; set; }
    public required string NombreArchivo { get; set; }
    public string? FormatoArchivo { get; set; }
    public string? RutaArchivo { get; set; }
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;
    public string EstadoValidacion { get; set; } = "pendiente";
    public string? Observaciones { get; set; }

    public Version? Version { get; set; }
    public Usuario? UsuarioCarga { get; set; }
    public ICollection<Metrica> Metricas { get; set; } = [];
    public ICollection<Evaluacion> Evaluaciones { get; set; } = [];
}
