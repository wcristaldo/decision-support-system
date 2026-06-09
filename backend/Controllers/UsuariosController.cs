using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DecisionSupportAPI.Data;
using System.Security.Claims;

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
                u.Id,
                u.Nombre,
                u.Email,
                u.Activo,
                u.FechaCreacion
            })
            .ToList();

        return Ok(usuarios);
    }
}
