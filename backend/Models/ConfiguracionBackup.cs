namespace DecisionSupportAPI.Models;

// RNF12: fila única de configuración del respaldo periódico de la BD.
public class ConfiguracionBackup
{
    public int Id { get; set; }
    public int IntervaloHoras { get; set; } = 24;
    public string CarpetaDestino { get; set; } = "backups";
    public bool Activo { get; set; } = true;
    public DateTime? FechaUltimaEjecucion { get; set; }
}
