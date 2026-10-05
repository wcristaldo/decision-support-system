using System.ComponentModel.DataAnnotations;

namespace DecisionSupportAPI.DTOs;

public class ResetPasswordConCodigoDto
{
    [Required, EmailAddress]
    public required string Email { get; set; }

    [Required, StringLength(6, MinimumLength = 6, ErrorMessage = "El código debe tener 6 dígitos.")]
    public required string Codigo { get; set; }

    [Required, MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public required string NewPassword { get; set; }
}
