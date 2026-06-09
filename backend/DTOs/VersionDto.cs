namespace DecisionSupportAPI.DTOs;

public class VersionDto
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string? NumeroVersion { get; set; }
    public string? Descripcion { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public class CreateVersionDto
{
    public int ProyectoId { get; set; }
    public required string NumeroVersion { get; set; }
    public string? Descripcion { get; set; }
}

public class UpdateVersionDto
{
    public string? NumeroVersion { get; set; }
    public string? Descripcion { get; set; }
}
