using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;
using DecisionSupportAPI.Services;

namespace DecisionSupportAPI.Tests;

/// <summary>
/// El módulo de pagos/suscripción está FUERA del alcance de RNF08 (no aparece
/// en ningún RF/RNF de la tesis) — estos fakes evitan que los tests de los
/// controllers/services SÍ alcanzados por RNF08 (Proyectos, Usuarios,
/// ResultadosPrueba, Analisis, etc.) tengan que sembrar una suscripción activa
/// completa solo para pasar el chequeo de límites de plan.
/// </summary>
public class FakeSuscripcionService : ISuscripcionService
{
    public Task<Suscripcion?> GetSuscripcionActivaAsync() => Task.FromResult<Suscripcion?>(null);
    public Task<PlanSuscripcion?> GetPlanActivoAsync() => Task.FromResult<PlanSuscripcion?>(null);
    public Task<LimiteVerificacion> VerificarLimiteProyectosAsync() => Task.FromResult(new LimiteVerificacion(true));
    public Task<LimiteVerificacion> VerificarLimiteUsuariosAsync() => Task.FromResult(new LimiteVerificacion(true));
    public Task<LimiteVerificacion> VerificarLimiteEvaluacionesMesAsync() => Task.FromResult(new LimiteVerificacion(true));
    public Task<LimiteVerificacion> VerificarTamanoArchivoAsync(long tamanoBytes) => Task.FromResult(new LimiteVerificacion(true));
    public Task<LimiteVerificacion> VerificarFeatureAsync(Func<PlanSuscripcion, bool> selector, string nombreFeature)
        => Task.FromResult(new LimiteVerificacion(true));
}

public class FakeEmailService : IEmailService
{
    public Task EnviarReciboAsync(ReciboData recibo, List<string> destinatarios, byte[] pdfBytes) => Task.CompletedTask;
    public Task EnviarAlertaNoAptoAsync(string proyectoNombre, string versionNumero, string archivoNombre, List<string> destinatarios) => Task.CompletedTask;
}

/// <summary>
/// Helpers compartidos para RNF08 (cobertura de tests de controllers/services
/// principales). Sigue el mismo patrón que RecommendationEngineTests.cs: un
/// ApplicationDbContext real respaldado por EF Core InMemory (sin librería de
/// mocks), y un ClaimsPrincipal armado a mano que imita exactamente lo que
/// DbClaimsTransformation.cs le agregaría a un usuario real autenticado
/// (roles, permisos "permission" y, desde RF13, "proyecto").
/// </summary>
public static class TestHelpers
{
    public static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public static ClaimsPrincipal BuildUser(
        int usuarioId,
        string[]? roles = null,
        string[]? permisos = null,
        int[]? proyectoIds = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuarioId.ToString()),
        };
        foreach (var rol in roles ?? Array.Empty<string>())
            claims.Add(new Claim(ClaimTypes.Role, rol));
        foreach (var permiso in permisos ?? Array.Empty<string>())
            claims.Add(new Claim("permission", permiso));
        // Administrador (rol) nunca recibe claims "proyecto" — sin restricción,
        // igual que en DbClaimsTransformation.cs.
        if (roles == null || !roles.Contains("Administrador"))
            foreach (var proyectoId in proyectoIds ?? Array.Empty<int>())
                claims.Add(new Claim("proyecto", proyectoId.ToString()));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>Asigna el ClaimsPrincipal al ControllerContext, como lo haría
    /// el pipeline real de ASP.NET Core tras la autenticación JWT.</summary>
    public static void SetUser(ControllerBase controller, ClaimsPrincipal user)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    /// <summary>Instancia REAL de AuditoriaService (no un fake) con un
    /// HttpContextAccessor mínimo — se usa en los tests de controllers para
    /// poder verificar, cuando corresponde, que la acción efectivamente quedó
    /// auditada (RNF05).</summary>
    public static AuditoriaService NewAuditoriaService(DecisionSupportAPI.Data.ApplicationDbContext ctx, ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext();
        if (user != null) httpContext.User = user;
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new AuditoriaService(ctx, accessor, Microsoft.Extensions.Logging.Abstractions.NullLogger<AuditoriaService>.Instance);
    }
}
