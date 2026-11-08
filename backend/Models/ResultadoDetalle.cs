namespace DecisionSupportAPI.Models;

/// <summary>
/// Resumen técnico (JSON compacto) de un resultado de pruebas: pruebas fallidas con su motivo, las más lentas y el
/// desglose por módulo. Va en una tabla aparte, una fila por resultado, para no ensanchar resultados_prueba y para que
/// los resultados cargados antes de esta función (o sin el archivo completo) simplemente no tengan detalle.
/// </summary>
public class ResultadoDetalle
{
    public int ResultadoId { get; set; }
    public required string DetalleJson { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public ResultadoPrueba? Resultado { get; set; }
}
