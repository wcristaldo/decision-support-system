namespace DecisionSupportAPI.DTOs;

public class ResultadoPruebaDto
{
    public int Id { get; set; }
    public int VersionId { get; set; }
    public int? UsuarioCargaId { get; set; }
    public string? NombreArchivo { get; set; }
    public string? FormatoArchivo { get; set; }
    public string? RutaArchivo { get; set; }
    public DateTime FechaCarga { get; set; }
    public string? EstadoValidacion { get; set; }
    public string? Observaciones { get; set; }
}

public class CreateResultadoPruebaDto
{
    public int VersionId { get; set; }
    public required string NombreArchivo { get; set; }
    public string? FormatoArchivo { get; set; }
    public string? RutaArchivo { get; set; }
    public string? Observaciones { get; set; }
}
