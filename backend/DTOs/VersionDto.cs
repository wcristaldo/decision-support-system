using System.ComponentModel.DataAnnotations;

namespace DecisionSupportAPI.DTOs;

public class VersionDto
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string? NumeroVersion { get; set; }
    public string? Descripcion { get; set; }
    public DateTime FechaVersion { get; set; }
    public string? Estado { get; set; }
}

public class CreateVersionDto
{
    public int ProyectoId { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "El número de versión es obligatorio.")]
    [StringLength(50)]
    [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "El número de versión debe usar formato semántico: mayor.menor.parche (ej: 1.0.0).")]
    public required string NumeroVersion { get; set; }

    [StringLength(255)]
    public string? Descripcion { get; set; }
}

public class UpdateVersionDto
{
    [StringLength(50)]
    [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "El número de versión debe usar formato semántico: mayor.menor.parche (ej: 1.0.0).")]
    public string? NumeroVersion { get; set; }

    [StringLength(255)]
    public string? Descripcion { get; set; }

    public string? Estado { get; set; }
}
