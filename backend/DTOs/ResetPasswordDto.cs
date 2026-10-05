using System.ComponentModel.DataAnnotations;

namespace DecisionSupportAPI.DTOs;

public class ResetPasswordDto
{
    [Required, MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public required string NewPassword { get; set; }
}
