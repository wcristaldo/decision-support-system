namespace DecisionSupportAPI.Models;

// Código numérico de 6 dígitos enviado por correo para autoservicio de
// restablecimiento de contraseña ("¿Olvidaste tu contraseña?" en Login).
public class CodigoResetPassword
{
    public int IdCodigo { get; set; }
    public int IdUsuario { get; set; }
    public required string Codigo { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaExpiracion { get; set; }
    public bool Usado { get; set; }

    public Usuario? Usuario { get; set; }
}
