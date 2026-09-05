using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;

namespace DecisionSupportAPI.Services;

/// <summary>Resultado uniforme de un intento de ingesta, para que cada controller
/// (carga manual desde la UI o ingesta automatizada CI/CD) lo traduzca a su propia
/// respuesta HTTP sin duplicar la lógica de negocio.</summary>
public record IngestaResultado(int StatusCode, object Body);

public interface IIngestaResultadosService
{
    Task<IngestaResultado> IngestarAsync(CreateResultadoPruebaDto request, int? usuarioCargaId = null);
}

/// <summary>
/// Pipeline único de ingesta de resultados de prueba (RF03-RF09): validar límites
/// de plan, persistir el resultado, calcular métricas, generar la recomendación
/// automática y notificar por email si corresponde. Usado tanto por la carga
/// manual (POST /api/ResultadosPrueba) como por la ingesta automatizada CI/CD
/// (POST /api/reports, CU-05 de la tesis).
/// </summary>
public class IngestaResultadosService : IIngestaResultadosService
{
    private readonly ApplicationDbContext _context;
    private readonly IMetricsCalculationService _metricsService;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly IAuditoriaService _auditoriaService;
    private readonly ISuscripcionService _suscripcionService;
    private readonly IEmailService _emailService;

    public IngestaResultadosService(
        ApplicationDbContext context,
        IMetricsCalculationService metricsService,
        IRecommendationEngine recommendationEngine,
        IAuditoriaService auditoriaService,
        ISuscripcionService suscripcionService,
        IEmailService emailService)
    {
        _context = context;
        _metricsService = metricsService;
        _recommendationEngine = recommendationEngine;
        _auditoriaService = auditoriaService;
        _suscripcionService = suscripcionService;
        _emailService = emailService;
    }

    public async Task<IngestaResultado> IngestarAsync(CreateResultadoPruebaDto request, int? usuarioCargaId = null)
    {
        // ── Verificar límites de plan ─────────────────────────────────────
        var limite = await _suscripcionService.VerificarLimiteEvaluacionesMesAsync();
        if (!limite.Permitido)
            return new IngestaResultado(402, new { message = limite.Mensaje, codigo = "LIMITE_EVALUACIONES" });

        if (request.TamanoBytes.HasValue)
        {
            var limiteArchivo = await _suscripcionService.VerificarTamanoArchivoAsync(request.TamanoBytes.Value);
            if (!limiteArchivo.Permitido)
                return new IngestaResultado(402, new { message = limiteArchivo.Mensaje, codigo = "LIMITE_TAMANO_ARCHIVO" });
        }

        // Validar coherencia de pruebas
        if (request.PruebasExitosas + request.PruebasFallidas > request.TotalPruebas)
            return new IngestaResultado(400, new { message = "La suma de exitosas y fallidas no puede superar el total de pruebas." });

        var version = await _context.Versiones.FirstOrDefaultAsync(v => v.Id == request.VersionId);
        if (version == null)
            return new IngestaResultado(404, new { message = "Versión no encontrada." });

        var resultado = new ResultadoPrueba
        {
            VersionId        = request.VersionId,
            UsuarioCargaId   = usuarioCargaId,
            NombreArchivo    = request.NombreArchivo,
            FormatoArchivo   = request.FormatoArchivo ?? "JSON",
            RutaArchivo      = request.RutaArchivo,
            Observaciones    = request.Observaciones,
            EstadoValidacion = "valido"    // se marca válido al ingresar los datos
        };

        _context.ResultadosPrueba.Add(resultado);
        await _context.SaveChangesAsync();

        // Calcular métricas reales y generar recomendación automática
        await _metricsService.CalculateMetricsAsync(
            resultado.Id,
            request.TotalPruebas,
            request.PruebasExitosas,
            request.PruebasFallidas,
            request.Cobertura,
            request.TiempoEjecucion,
            request.PruebasOmitidas);

        await _recommendationEngine.GenerateRecommendationForResultadoAsync(resultado.Id);

        // ── Notificación automática si plan lo permite ────────────────────
        try
        {
            var plan = await _suscripcionService.GetPlanActivoAsync();
            if (plan?.NotificacionesEmail == true)
            {
                var ultimaRec = await _context.Recomendaciones
                    .Include(r => r.Evaluacion)
                    .Where(r => r.Evaluacion != null && r.Evaluacion.ResultadoId == resultado.Id)
                    .OrderByDescending(r => r.FechaGeneracion)
                    .FirstOrDefaultAsync();

                if (ultimaRec?.TipoRecomendacion == "no_desplegar")
                {
                    var ver = await _context.Versiones
                        .Include(v => v.Proyecto)
                        .FirstOrDefaultAsync(v => v.Id == request.VersionId);

                    var adminEmails = await _context.Usuarios
                        .Where(u => u.Estado == "activo"
                                 && u.UsuarioRoles.Any(ur => ur.Estado == "activo"
                                     && ur.Rol != null
                                     && ur.Rol.NombreRol == "Administrador"))
                        .Select(u => u.Email)
                        .ToListAsync();

                    if (adminEmails.Count > 0 && ver != null)
                    {
                        await _emailService.EnviarAlertaNoAptoAsync(
                            ver.Proyecto?.Nombre ?? "—",
                            ver.NumeroVersion,
                            request.NombreArchivo,
                            adminEmails);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // La notificación no debe interrumpir el flujo principal
            await _auditoriaService.RegistrarAsync("Error", "ResultadoPrueba", resultado.Id,
                $"Error al enviar alerta email: {ex.Message}");
        }

        await _auditoriaService.RegistrarAsync("Create", "ResultadoPrueba", resultado.Id,
            $"VersionId: {request.VersionId}, Archivo: {request.NombreArchivo}, " +
            $"Total: {request.TotalPruebas}, Exitosas: {request.PruebasExitosas}, " +
            $"Cobertura: {request.Cobertura}%");

        var dto = new ResultadoPruebaDto
        {
            Id               = resultado.Id,
            VersionId        = resultado.VersionId,
            UsuarioCargaId   = resultado.UsuarioCargaId,
            NombreArchivo    = resultado.NombreArchivo,
            FormatoArchivo   = resultado.FormatoArchivo,
            RutaArchivo      = resultado.RutaArchivo,
            FechaCarga       = resultado.FechaCarga,
            EstadoValidacion = resultado.EstadoValidacion,
            Observaciones    = resultado.Observaciones
        };

        return new IngestaResultado(201, dto);
    }
}
