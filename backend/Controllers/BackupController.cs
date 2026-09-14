using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Controllers;

/// <summary>
/// RNF12: configuración y disparo manual del respaldo periódico de la base de
/// datos. Solo Administrador (gestionar_usuarios, la misma política que ya
/// protege el resto de la administración del sistema).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "gestionar_usuarios")]
public class BackupController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseBackupService _backupService;
    private readonly IAuditoriaService _auditoriaService;

    public BackupController(ApplicationDbContext context, DatabaseBackupService backupService, IAuditoriaService auditoriaService)
    {
        _context = context;
        _backupService = backupService;
        _auditoriaService = auditoriaService;
    }

    public record ConfiguracionBackupRequest(int IntervaloHoras, string CarpetaDestino, bool Activo);

    [HttpGet("configuracion")]
    public async Task<IActionResult> GetConfiguracion()
    {
        var config = await _context.ConfiguracionesBackup.FirstOrDefaultAsync();
        if (config == null) return NotFound(new { message = "No hay configuración de respaldo inicializada." });

        return Ok(new
        {
            config.IntervaloHoras,
            config.CarpetaDestino,
            config.Activo,
            config.FechaUltimaEjecucion,
        });
    }

    [HttpPut("configuracion")]
    public async Task<IActionResult> ActualizarConfiguracion([FromBody] ConfiguracionBackupRequest request)
    {
        if (request.IntervaloHoras < 1)
            return BadRequest(new { message = "El intervalo debe ser de al menos 1 hora." });
        if (string.IsNullOrWhiteSpace(request.CarpetaDestino))
            return BadRequest(new { message = "La carpeta destino es obligatoria." });

        var config = await _context.ConfiguracionesBackup.FirstOrDefaultAsync();
        if (config == null) return NotFound(new { message = "No hay configuración de respaldo inicializada." });

        config.IntervaloHoras = request.IntervaloHoras;
        config.CarpetaDestino = request.CarpetaDestino.Trim();
        config.Activo = request.Activo;
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync("Update", "ConfiguracionBackup", config.Id,
            $"Intervalo: {config.IntervaloHoras}h, Carpeta: {config.CarpetaDestino}, Activo: {config.Activo}");

        return Ok(new { message = "Configuración de respaldo actualizada correctamente." });
    }

    [HttpPost("ejecutar-ahora")]
    public async Task<IActionResult> EjecutarAhora()
    {
        var config = await _context.ConfiguracionesBackup.FirstOrDefaultAsync();
        if (config == null) return NotFound(new { message = "No hay configuración de respaldo inicializada." });

        try
        {
            var ruta = await _backupService.EjecutarBackupAsync(_context, config, disparadoManualmente: true, HttpContext.RequestAborted);
            await _auditoriaService.RegistrarAsync("Create", "ConfiguracionBackup", config.Id, $"Respaldo manual generado: {Path.GetFileName(ruta)}");
            return Ok(new { message = "Respaldo generado correctamente.", archivo = Path.GetFileName(ruta) });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "No se pudo generar el respaldo.", error = ex.Message });
        }
    }

    [HttpGet("historial")]
    public async Task<IActionResult> GetHistorial()
    {
        var config = await _context.ConfiguracionesBackup.FirstOrDefaultAsync();
        if (config == null) return Ok(new List<object>());

        var carpeta = Path.IsPathRooted(config.CarpetaDestino)
            ? config.CarpetaDestino
            : Path.Combine(AppContext.BaseDirectory, config.CarpetaDestino);

        if (!Directory.Exists(carpeta))
            return Ok(new List<object>());

        var archivos = Directory.GetFiles(carpeta, "backup_*.sql")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => new { nombre = f.Name, tamanoBytes = f.Length, fecha = f.CreationTimeUtc })
            .ToList();

        return Ok(archivos);
    }
}
