using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;

namespace DecisionSupportAPI.Services;

/// <summary>
/// RNF12: "El sistema debe contar con un mecanismo de respaldo periódico de la
/// base de datos, configurable por el Administrador, para preservar la
/// integridad de los datos históricos." Corre en background durante toda la
/// vida del proceso; cada POLL_INTERVAL revisa configuracion_backup (fila
/// única, editable en runtime desde BackupController) y dispara un pg_dump
/// real si ya pasó el intervalo configurado desde la última ejecución.
/// </summary>
public class DatabaseBackupService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<DatabaseBackupService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EjecutarSiCorrespondeAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el ciclo del respaldo periódico de la base de datos.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Apagado normal de la aplicación.
            }
        }
    }

    private async Task EjecutarSiCorrespondeAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var config = await context.ConfiguracionesBackup.FirstOrDefaultAsync(stoppingToken);
        if (config == null || !config.Activo)
            return;

        var proximaEjecucion = (config.FechaUltimaEjecucion ?? DateTime.MinValue).AddHours(config.IntervaloHoras);
        if (DateTime.UtcNow < proximaEjecucion)
            return;

        await EjecutarBackupAsync(context, config, disparadoManualmente: false, stoppingToken);
    }

    /// <summary>Ejecuta un pg_dump real ahora mismo (llamado tanto por el ciclo
    /// automático como por POST /api/backup/ejecutar-ahora) y actualiza
    /// fecha_ultima_ejecucion. Devuelve la ruta del archivo generado.</summary>
    public async Task<string> EjecutarBackupAsync(ApplicationDbContext context, ConfiguracionBackup config, bool disparadoManualmente, CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection no configurada.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        var carpeta = Path.IsPathRooted(config.CarpetaDestino)
            ? config.CarpetaDestino
            : Path.Combine(AppContext.BaseDirectory, config.CarpetaDestino);
        Directory.CreateDirectory(carpeta);

        var nombreArchivo = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql";
        var rutaCompleta = Path.Combine(carpeta, nombreArchivo);

        var pgDumpPath = _configuration["Backup:PgDumpPath"] ?? "pg_dump";

        var psi = new ProcessStartInfo
        {
            FileName = pgDumpPath,
            ArgumentList =
            {
                "-h", builder.Host ?? "localhost",
                "-p", (builder.Port == 0 ? 5432 : builder.Port).ToString(),
                "-U", builder.Username ?? "postgres",
                "-F", "p",
                "-f", rutaCompleta,
                builder.Database ?? "postgres",
            },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        psi.Environment["PGPASSWORD"] = builder.Password ?? "";

        using var proceso = Process.Start(psi)
            ?? throw new InvalidOperationException("No se pudo iniciar pg_dump.");
        var stderr = await proceso.StandardError.ReadToEndAsync(cancellationToken);
        await proceso.WaitForExitAsync(cancellationToken);

        if (proceso.ExitCode != 0)
        {
            _logger.LogError("pg_dump terminó con código {Codigo}: {Error}", proceso.ExitCode, stderr);
            throw new InvalidOperationException($"pg_dump falló (código {proceso.ExitCode}): {stderr}");
        }

        config.FechaUltimaEjecucion = DateTime.UtcNow;
        context.ConfiguracionesBackup.Update(config);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Respaldo de base de datos generado: {Ruta} ({Origen})", rutaCompleta, disparadoManualmente ? "manual" : "automático");
        return rutaCompleta;
    }
}
