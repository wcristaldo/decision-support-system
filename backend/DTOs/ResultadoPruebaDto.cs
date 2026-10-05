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

    [Required, StringLength(255)]
    public required string NombreArchivo { get; set; }

    [StringLength(20)]
    public string? FormatoArchivo { get; set; } = "JSON";

    [StringLength(255)]
    public string? RutaArchivo { get; set; }

    [StringLength(255)]
    public string? Observaciones { get; set; }

    // ── Métricas reales del reporte de pruebas ────────────────────────────────
    // Mínimo 1: un resultado con 0 pruebas no representa una ejecución real (0%
    // de éxito por ausencia de datos, no por fallas) y confundiría al motor de
    // recomendación. El frontend (CargarResultados.jsx) ya exige esto mismo.
    [Required, Range(1, int.MaxValue, ErrorMessage = "El total de pruebas debe ser al menos 1.")]
    public int TotalPruebas { get; set; }

    [Required, Range(0, int.MaxValue)]
    public int PruebasExitosas { get; set; }

    [Required, Range(0, int.MaxValue)]
    public int PruebasFallidas { get; set; }

    /// <summary>Pruebas omitidas/saltadas (RF06). Opcional por compatibilidad con integraciones existentes.</summary>
    [Range(0, int.MaxValue)]
    public int PruebasOmitidas { get; set; } = 0;

    /// <summary>Cobertura de ejecución de pruebas: pruebas ejecutadas (exitosas + fallidas) sobre el total, en porcentaje (0-100).</summary>
    [Required, Range(0, 100)]
    public decimal Cobertura { get; set; }

    /// <summary>Tiempo total de ejecución en segundos.</summary>
    [Required, Range(0, double.MaxValue)]
    public decimal TiempoEjecucion { get; set; }

    /// <summary>
    /// Tamaño del archivo JSON en bytes (opcional).
    /// Si se incluye, se valida contra el límite del plan activo.
    /// </summary>
    public long? TamanoBytes { get; set; }
}
