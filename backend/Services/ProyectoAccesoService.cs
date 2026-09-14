using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Models;

namespace DecisionSupportAPI.Services;

/// <summary>
/// RF13: centraliza la verificación de acceso por proyecto. Administrador no
/// tiene ninguna restricción (no recibe claims "proyecto", ver
/// DbClaimsTransformation.cs); el resto de los roles solo puede ver/operar
/// los proyectos a los que fue asignado explícitamente (tabla
/// usuario_proyecto). Esto es una capa ADICIONAL sobre el RBAC por permisos
/// (RF14) que ya existía — un usuario necesita AMBAS cosas: el permiso
/// correspondiente (ej. "ver_proyectos") Y el acceso al proyecto puntual.
/// </summary>
public interface IProyectoAccesoService
{
    bool EsIrrestricto(ClaimsPrincipal user);
    List<int> ProyectosPermitidos(ClaimsPrincipal user);
    IQueryable<Proyecto> FiltrarProyectos(IQueryable<Proyecto> query, ClaimsPrincipal user);
    IQueryable<Models.Version> FiltrarVersiones(IQueryable<Models.Version> query, ClaimsPrincipal user);
    bool TieneAccesoAProyecto(ClaimsPrincipal user, int proyectoId);
    Task<bool> TieneAccesoAVersionAsync(ClaimsPrincipal user, int versionId);
    Task<bool> TieneAccesoAResultadoAsync(ClaimsPrincipal user, int resultadoId);
    Task<bool> TieneAccesoAEvaluacionAsync(ClaimsPrincipal user, int evaluacionId);
    Task<bool> TieneAccesoARecomendacionAsync(ClaimsPrincipal user, int recomendacionId);
}

public class ProyectoAccesoService : IProyectoAccesoService
{
    private readonly ApplicationDbContext _context;

    public ProyectoAccesoService(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool EsIrrestricto(ClaimsPrincipal user) => !user.FindAll("proyecto").Any() && user.Identity?.IsAuthenticated == true
        // Un usuario autenticado sin NINGÚN claim "proyecto" es Administrador
        // (DbClaimsTransformation solo omite ese claim para ese rol). Si en el
        // futuro se agrega un rol nuevo sin proyectos asignados, este método
        // seguiría tratándolo como irrestricto salvo que se verifique el rol
        // explícitamente — por eso se refuerza con el chequeo de rol abajo.
        && user.IsInRole("Administrador");

    public List<int> ProyectosPermitidos(ClaimsPrincipal user) =>
        user.FindAll("proyecto").Select(c => int.Parse(c.Value)).ToList();

    public IQueryable<Proyecto> FiltrarProyectos(IQueryable<Proyecto> query, ClaimsPrincipal user)
    {
        if (EsIrrestricto(user)) return query;
        var permitidos = ProyectosPermitidos(user);
        return query.Where(p => permitidos.Contains(p.Id));
    }

    public IQueryable<Models.Version> FiltrarVersiones(IQueryable<Models.Version> query, ClaimsPrincipal user)
    {
        if (EsIrrestricto(user)) return query;
        var permitidos = ProyectosPermitidos(user);
        return query.Where(v => permitidos.Contains(v.ProyectoId));
    }

    public bool TieneAccesoAProyecto(ClaimsPrincipal user, int proyectoId)
    {
        if (EsIrrestricto(user)) return true;
        return ProyectosPermitidos(user).Contains(proyectoId);
    }

    public async Task<bool> TieneAccesoAVersionAsync(ClaimsPrincipal user, int versionId)
    {
        if (EsIrrestricto(user)) return true;
        var proyectoId = await _context.Versiones
            .Where(v => v.Id == versionId)
            .Select(v => (int?)v.ProyectoId)
            .FirstOrDefaultAsync();
        return proyectoId != null && TieneAccesoAProyecto(user, proyectoId.Value);
    }

    public async Task<bool> TieneAccesoAResultadoAsync(ClaimsPrincipal user, int resultadoId)
    {
        if (EsIrrestricto(user)) return true;
        var proyectoId = await _context.ResultadosPrueba
            .Where(r => r.Id == resultadoId)
            .Join(_context.Versiones, r => r.VersionId, v => v.Id, (r, v) => (int?)v.ProyectoId)
            .FirstOrDefaultAsync();
        return proyectoId != null && TieneAccesoAProyecto(user, proyectoId.Value);
    }

    public async Task<bool> TieneAccesoAEvaluacionAsync(ClaimsPrincipal user, int evaluacionId)
    {
        if (EsIrrestricto(user)) return true;
        var resultadoId = await _context.Evaluaciones
            .Where(e => e.Id == evaluacionId)
            .Select(e => (int?)e.ResultadoId)
            .FirstOrDefaultAsync();
        return resultadoId != null && await TieneAccesoAResultadoAsync(user, resultadoId.Value);
    }

    public async Task<bool> TieneAccesoARecomendacionAsync(ClaimsPrincipal user, int recomendacionId)
    {
        if (EsIrrestricto(user)) return true;
        var evaluacionId = await _context.Recomendaciones
            .Where(r => r.Id == recomendacionId)
            .Select(r => (int?)r.EvaluacionId)
            .FirstOrDefaultAsync();
        return evaluacionId != null && await TieneAccesoAEvaluacionAsync(user, evaluacionId.Value);
    }
}
