namespace DecisionSupportAPI.DTOs;

public class AnalisisHistorialDto
{
    public int    ProyectoId       { get; set; }
    public string ProyectoNombre   { get; set; } = "";
    public string ProyectoTipo     { get; set; } = "";
    public int    VersionId        { get; set; }
    public string VersionNumero    { get; set; } = "";
    public int    RecomendacionId  { get; set; }
    public string Recomendacion    { get; set; } = "";
    public string? Justificacion   { get; set; }
    public DateTime FechaCarga      { get; set; }
    public DateTime FechaEvaluacion { get; set; }

    // Métricas clave
    public decimal? TasaExito       { get; set; }
    public decimal? TasaFallo       { get; set; }
    public decimal? Cobertura       { get; set; }
    public decimal? TiempoEjecucion { get; set; }
    public decimal? TotalPruebas    { get; set; }
    public decimal? PruebasExitosas { get; set; }
    public decimal? PruebasFallidas { get; set; }
}
