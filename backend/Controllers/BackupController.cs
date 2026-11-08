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

    private static string ResolverCarpeta(string carpetaDestino)
        => Path.IsPathRooted(carpetaDestino)
            ? carpetaDestino
            : Path.Combine(AppContext.BaseDirectory, carpetaDestino);

    /// <summary>
    /// GET /api/backup/descargar/{nombre}
    /// Descarga un respaldo ya generado. Solo se sirven archivos "backup_*.sql"
    /// que estén en la carpeta destino configurada: el nombre no puede contener
    /// rutas, así que no se puede salir de esa carpeta. Queda registrado en la
    /// auditoría porque el archivo contiene todos los datos del sistema.
    /// </summary>
    [HttpGet("descargar/{nombre}")]
    public async Task<IActionResult> Descargar(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)
            || nombre != Path.GetFileName(nombre)
            || !System.Text.RegularExpressions.Regex.IsMatch(nombre, @"^backup_[A-Za-z0-9_\-]+\.sql$"))
            return BadRequest(new { message = "Nombre de respaldo inválido." });

        var config = await _context.ConfiguracionesBackup.FirstOrDefaultAsync();
        if (config == null) return NotFound(new { message = "No hay configuración de respaldo inicializada." });

        var ruta = Path.Combine(ResolverCarpeta(config.CarpetaDestino), nombre);
        if (!System.IO.File.Exists(ruta))
            return NotFound(new { message = "El respaldo solicitado no existe." });

        await _auditoriaService.RegistrarAsync("Download", "ConfiguracionBackup", config.Id, $"Respaldo descargado: {nombre}");
        return PhysicalFile(ruta, "application/sql", nombre);
    }

    [HttpGet("historial")]
    public async Task<IActionResult> GetHistorial()
    {
        var config = await _context.ConfiguracionesBackup.FirstOrDefaultAsync();
        if (config == null) return Ok(new List<object>());

        var carpeta = ResolverCarpeta(config.CarpetaDestino);

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
