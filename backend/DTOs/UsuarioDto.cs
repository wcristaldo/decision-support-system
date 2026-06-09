namespace DecisionSupportAPI.DTOs;

public class UsuarioDto
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public string? Email { get; set; }
    public bool Activo { get; set; }
    public List<string> Roles { get; set; } = [];
    public List<string> Permisos { get; set; } = [];
}
