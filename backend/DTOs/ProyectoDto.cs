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
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
    public string? TipoSolucion { get; set; }
    public string? VersionInicial { get; set; }
}

public class UpdateProyectoDto
{
    public string? Nombre { get; set; }
    public string? Descripcion { get; set; }
    public string? TipoSolucion { get; set; }
    public string? Estado { get; set; }
}
