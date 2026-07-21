using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DecisionSupportAPI.Data;

namespace DecisionSupportAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public RolesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET /api/roles — devuelve todos los roles activos
    [HttpGet]
    public IActionResult GetRoles()
    {
        var roles = _context.Roles
            .Where(r => r.Estado == "activo")
            .OrderBy(r => r.NombreRol)
            .Select(r => new { r.IdRol, r.NombreRol })
            .ToList();

        return Ok(roles);
    }
}
