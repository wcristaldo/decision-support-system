namespace DecisionSupportAPI.DTOs;

public class ResultadoPruebaDto
{
    public int Id { get; set; }
    public int VersionId { get; set; }
    public string? NombrePrueba { get; set; }
    public string? TipoPrueba { get; set; }
    public string? Estado { get; set; }
    public decimal? TiempoEjecucion { get; set; }
    public string? Mensaje { get; set; }
    public DateTime FechaEjecucion { get; set; }
}

public class CreateResultadoPruebaDto
{
    public int VersionId { get; set; }
    public required string NombrePrueba { get; set; }
    public string? TipoPrueba { get; set; }
    public string? Estado { get; set; }
    public decimal? TiempoEjecucion { get; set; }
    public string? Mensaje { get; set; }
}

public class UploadResultadosPruebaDto
{
    public int VersionId { get; set; }
    public required List<CreateResultadoPruebaDto> Resultados { get; set; }
}
