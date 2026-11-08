namespace DecisionSupportAPI.DTOs;

/// <summary>
/// Filtros del historial de evaluaciones (RF11) que la pantalla "Análisis y métricas"
/// envía al exportar, para que el archivo contenga solo lo que el usuario está viendo.
/// Todos son opcionales; sin ninguno se exporta el historial completo.
/// </summary>
public class HistorialFiltroDto
{
    /// <summary>DESPLEGAR, REVISAR o NO_DESPLEGAR (vacío o "Todos" = sin filtro).</summary>
    public string? Recomendacion { get; set; }
    /// <summary>Nombre exacto del proyecto.</summary>
    public string? Proyecto { get; set; }
    /// <summary>Nombre del usuario que cargó el resultado.</summary>
    public string? Responsable { get; set; }
    /// <summary>Fecha de carga mínima, formato yyyy-MM-dd.</summary>
    public string? Desde { get; set; }
    /// <summary>Fecha de carga máxima, formato yyyy-MM-dd.</summary>
    public string? Hasta { get; set; }
}

public class AnalisisHistorialDto
{
    public int    ProyectoId       { get; set; }
    public string ProyectoNombre   { get; set; } = "";
    public string ProyectoTipo     { get; set; } = "";
    public int    VersionId        { get; set; }
    public string VersionNumero    { get; set; } = "";
    public int    ResultadoId       { get; set; }
    public int    RecomendacionId  { get; set; }
    public string Recomendacion    { get; set; } = "";
    public string? Justificacion   { get; set; }
    public DateTime FechaCarga      { get; set; }
    public DateTime FechaEvaluacion { get; set; }
    public string? UsuarioCargaNombre { get; set; }

    // Métricas clave
    public decimal? TasaExito       { get; set; }
    public decimal? TasaFallo       { get; set; }
    public decimal? Cobertura       { get; set; }
    public decimal? TiempoEjecucion { get; set; }
    public decimal? TotalPruebas    { get; set; }
    public decimal? PruebasExitosas { get; set; }
    public decimal? PruebasFallidas { get; set; }
}
