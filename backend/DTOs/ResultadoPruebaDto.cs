using System.ComponentModel.DataAnnotations;

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
    [Required]
    public int VersionId { get; set; }

    [Required]
    public required string NombreArchivo { get; set; }

    public string? FormatoArchivo { get; set; } = "JSON";
    public string? RutaArchivo { get; set; }
    public string? Observaciones { get; set; }

    // ── Métricas reales del reporte de pruebas ────────────────────────────────
    [Required, Range(0, int.MaxValue)]
    public int TotalPruebas { get; set; }

    [Required, Range(0, int.MaxValue)]
    public int PruebasExitosas { get; set; }

    [Required, Range(0, int.MaxValue)]
    public int PruebasFallidas { get; set; }

    /// <summary>Porcentaje de cobertura de código (0-100).</summary>
    [Required, Range(0, 100)]
    public decimal Cobertura { get; set; }

    /// <summary>Tiempo total de ejecución en segundos.</summary>
    [Required, Range(0, double.MaxValue)]
    public decimal TiempoEjecucion { get; set; }
}
