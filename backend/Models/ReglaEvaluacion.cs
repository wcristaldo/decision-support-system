namespace DecisionSupportAPI.Models;

public class ReglaEvaluacion
{
    public int Id { get; set; }
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    public string? Criterio { get; set; }
    public decimal? Umbral { get; set; }
    public string Estado { get; set; } = "activo";
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public int? UsuarioCreacionId { get; set; }

    /// <summary>
    /// Proyecto al que aplica esta regla. NULL = regla global (umbral por defecto,
    /// RF08); con valor = override específico del proyecto (RF07/CU-03: umbrales
    /// configurables por proyecto).
    /// </summary>
    public int? ProyectoId { get; set; }

    public Usuario? UsuarioCreacion { get; set; }
    public Proyecto? Proyecto { get; set; }
    public ICollection<EvaluacionRegla> EvaluacionReglas { get; set; } = [];
}
