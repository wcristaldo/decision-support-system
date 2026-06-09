namespace DecisionSupportAPI.DTOs;

public class LoginResponseDto
{
    public required string Token { get; set; }
    public required UsuarioDto Usuario { get; set; }
}
