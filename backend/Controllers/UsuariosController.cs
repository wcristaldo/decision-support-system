using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DecisionSupportAPI.Data;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public UsuariosController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult GetUsuarios()
    {
        if (!User.IsInRole("ADMIN") && !User.IsInRole("Administrador"))
            return Forbid();

        var usuarios = _context.Usuarios
            .Select(u => new
            {
                Id = u.IdUsuario,
                u.Nombre,
                u.Apellido,
                u.Email,
                Activo = u.Estado == "activo",
                u.Estado,
                u.FechaCreacion
            })
            .ToList();

        return Ok(usuarios);
    }
}
