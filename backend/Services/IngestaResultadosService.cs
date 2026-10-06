using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.DTOs;
using DecisionSupportAPI.Models;

namespace DecisionSupportAPI.Services;

/// <summary>Resultado uniforme de un intento de ingesta, para que cada controller
/// (carga manual desde la UI o ingesta automatizada por API) lo traduzca a su propia
/// respuesta HTTP sin duplicar la lógica de negocio.</summary>
public record IngestaResultado(int StatusCode, object Body);

public interface IIngestaResultadosService
{
    Task<IngestaResultado> IngestarAsync(CreateResultadoPruebaDto request, int? usuarioCargaId = null);
}

/// <summary>
/// Flujo único de ingesta de resultados de prueba (RF03-RF09): validar límites
/// de plan, persistir el resultado, calcular métricas, generar la recomendación
/// automática y notificar por email si corresponde. Usado tanto por la carga
/// manual (POST /api/ResultadosPrueba) como por la ingesta automatizada por API
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

        // Validar coherencia de pruebas: el total debe ser EXACTAMENTE la suma de
        // exitosas + fallidas + omitidas (antes solo se chequeaba que no lo superara,
        // permitiendo enviar métricas incoherentes, ej. total=100 con solo 15
        // pruebas contabilizadas entre las tres categorías).
        if (request.PruebasExitosas + request.PruebasFallidas + request.PruebasOmitidas != request.TotalPruebas)
            return new IngestaResultado(400, new { message = "El total de pruebas debe ser igual a la suma de exitosas, fallidas y omitidas." });

        // La cobertura es un valor derivado de los propios conteos — (exitosas+fallidas)/total —
        // no un dato independiente. Se recalcula en el servidor en vez de confiar en el valor
        // que mande el cliente, para que un POST directo a la API (sin pasar por el parser de
        // Robot Framework del frontend) no pueda declarar una cobertura arbitraria/falsa.
        var coberturaCalculada = request.TotalPruebas > 0
            ? Math.Round((request.PruebasExitosas + request.PruebasFallidas) / (decimal)request.TotalPruebas * 100, 2)
            : 0;

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

        // El resultado, sus métricas y la recomendación se persisten como una sola
        // unidad atómica: antes eran 3 operaciones independientes (cada una con su
        // propio SaveChanges) y un fallo entre medio dejaba un resultado sin
        // métricas ni recomendación, visible igual para otros endpoints.
        // El proveedor InMemory (usado en las pruebas unitarias, RNF08) no soporta
        // transacciones explícitas y lanza si se le pide una — en producción
        // (Npgsql, relacional) sí se abre la transacción real.
        var soportaTransacciones = _context.Database.IsRelational();
        await using var transaction = soportaTransacciones ? await _context.Database.BeginTransactionAsync() : null;

        _context.ResultadosPrueba.Add(resultado);
        await _context.SaveChangesAsync();

        await _metricsService.CalculateMetricsAsync(
            resultado.Id,
            request.TotalPruebas,
            request.PruebasExitosas,
            request.PruebasFallidas,
            coberturaCalculada,
            request.TiempoEjecucion,
            request.PruebasOmitidas);

        await _recommendationEngine.GenerateRecommendationForResultadoAsync(resultado.Id);

        if (transaction != null)
            await transaction.CommitAsync();

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
            $"Cobertura: {coberturaCalculada.ToString(System.Globalization.CultureInfo.InvariantCulture)}%");

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
