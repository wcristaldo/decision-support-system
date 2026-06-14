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

    public Usuario? UsuarioCreacion { get; set; }
    public ICollection<EvaluacionRegla> EvaluacionReglas { get; set; } = [];
}
