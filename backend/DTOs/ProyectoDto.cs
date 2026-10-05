using System.ComponentModel.DataAnnotations;

namespace DecisionSupportAPI.DTOs;

public class ProyectoDto
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public string? Descripcion { get; set; }
    public string? TipoSolucion { get; set; }
    public string Estado { get; set; } = "activo";
    public DateTime FechaCreacion { get; set; }
}

public class CreateProyectoDto
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150)]
    public required string Nombre { get; set; }

    [StringLength(255)]
    public string? Descripcion { get; set; }

    [StringLength(100)]
    public string? TipoSolucion { get; set; }

    [StringLength(50)]
    public string? VersionInicial { get; set; }
}

public class UpdateProyectoDto
{
    [StringLength(150)]
    public string? Nombre { get; set; }

    [StringLength(255)]
    public string? Descripcion { get; set; }

    [StringLength(100)]
    public string? TipoSolucion { get; set; }

    public string? Estado { get; set; }
}
