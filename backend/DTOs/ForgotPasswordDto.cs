using System.ComponentModel.DataAnnotations;

namespace DecisionSupportAPI.DTOs;

public class ForgotPasswordDto
{
    [Required, EmailAddress]
    public required string Email { get; set; }
}
