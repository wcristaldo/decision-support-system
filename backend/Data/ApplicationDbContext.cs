using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Models;

namespace DecisionSupportAPI.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Usuario>          Usuarios           { get; set; }
    public DbSet<Rol>              Roles              { get; set; }
    public DbSet<Permiso>          Permisos           { get; set; }
    public DbSet<RolPermiso>       RolPermisos        { get; set; }
    public DbSet<UsuarioRol>       UsuarioRoles       { get; set; }
    public DbSet<Proyecto>         Proyectos          { get; set; }
    public DbSet<Models.Version>   Versiones          { get; set; }
    public DbSet<ReglaEvaluacion>  ReglasEvaluacion   { get; set; }
    public DbSet<ResultadoPrueba>  ResultadosPrueba   { get; set; }
    public DbSet<Metrica>          Metricas           { get; set; }
    public DbSet<Evaluacion>       Evaluaciones       { get; set; }
    public DbSet<EvaluacionRegla>  EvaluacionReglas   { get; set; }
    public DbSet<Recomendacion>    Recomendaciones    { get; set; }
    public DbSet<DecisionDespliegue> DecisionesDespliegue { get; set; }
    public DbSet<Auditoria>        Auditoria          { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── USUARIOS ────────────────────────────────────────────
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios");
            e.HasKey(u => u.IdUsuario);
            e.Property(u => u.IdUsuario).HasColumnName("id_usuario");
            e.Property(u => u.Nombre).HasColumnName("nombre");
            e.Property(u => u.Apellido).HasColumnName("apellido");
            e.Property(u => u.Email).HasColumnName("email");
            e.Property(u => u.PasswordHash).HasColumnName("password_hash");
            e.Property(u => u.Estado).HasColumnName("estado");
            e.Property(u => u.FechaCreacion).HasColumnName("fecha_creacion");
            e.Property(u => u.FechaUltimoAcceso).HasColumnName("fecha_ultimo_acceso");
            e.HasIndex(u => u.Email).IsUnique();
        });

        // ── ROLES ────────────────────────────────────────────────
        modelBuilder.Entity<Rol>(e =>
        {
            e.ToTable("roles");
            e.HasKey(r => r.IdRol);
            e.Property(r => r.IdRol).HasColumnName("id_rol");
            e.Property(r => r.NombreRol).HasColumnName("nombre_rol");
            e.Property(r => r.Descripcion).HasColumnName("descripcion");
            e.Property(r => r.Estado).HasColumnName("estado");
            e.HasIndex(r => r.NombreRol).IsUnique();
        });

        // ── PERMISOS ─────────────────────────────────────────────
        modelBuilder.Entity<Permiso>(e =>
        {
            e.ToTable("permisos");
            e.HasKey(p => p.IdPermiso);
            e.Property(p => p.IdPermiso).HasColumnName("id_permiso");
            e.Property(p => p.NombrePermiso).HasColumnName("nombre_permiso");
            e.Property(p => p.Descripcion).HasColumnName("descripcion");
            e.Property(p => p.Modulo).HasColumnName("modulo");
            e.HasIndex(p => p.NombrePermiso).IsUnique();
        });

        // ── ROL_PERMISO ──────────────────────────────────────────
        modelBuilder.Entity<RolPermiso>(e =>
        {
            e.ToTable("rol_permiso");
            e.HasKey(rp => rp.IdRolPermiso);
            e.Property(rp => rp.IdRolPermiso).HasColumnName("id_rol_permiso");
            e.Property(rp => rp.IdRol).HasColumnName("id_rol");
            e.Property(rp => rp.IdPermiso).HasColumnName("id_permiso");
            e.HasOne(rp => rp.Rol).WithMany(r => r.RolPermisos).HasForeignKey(rp => rp.IdRol);
            e.HasOne(rp => rp.Permiso).WithMany(p => p.RolPermisos).HasForeignKey(rp => rp.IdPermiso);
        });

        // ── USUARIO_ROL ──────────────────────────────────────────
        modelBuilder.Entity<UsuarioRol>(e =>
        {
            e.ToTable("usuario_rol");
            e.HasKey(ur => ur.IdUsuarioRol);
            e.Property(ur => ur.IdUsuarioRol).HasColumnName("id_usuario_rol");
            e.Property(ur => ur.IdUsuario).HasColumnName("id_usuario");
            e.Property(ur => ur.IdRol).HasColumnName("id_rol");
            e.Property(ur => ur.FechaAsignacion).HasColumnName("fecha_asignacion");
            e.Property(ur => ur.Estado).HasColumnName("estado");
            e.HasOne(ur => ur.Usuario).WithMany(u => u.UsuarioRoles).HasForeignKey(ur => ur.IdUsuario);
            e.HasOne(ur => ur.Rol).WithMany(r => r.UsuarioRoles).HasForeignKey(ur => ur.IdRol);
        });

        // ── PROYECTOS ────────────────────────────────────────────
        modelBuilder.Entity<Proyecto>(e =>
        {
            e.ToTable("proyectos");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id_proyecto");
            e.Property(p => p.Nombre).HasColumnName("nombre_proyecto");
            e.Property(p => p.Descripcion).HasColumnName("descripcion");
            e.Property(p => p.TipoSolucion).HasColumnName("tipo_solucion");
            e.Property(p => p.Estado).HasColumnName("estado");
            e.Property(p => p.FechaCreacion).HasColumnName("fecha_creacion");
        });

        // ── VERSIONES ────────────────────────────────────────────
        modelBuilder.Entity<Models.Version>(e =>
        {
            e.ToTable("versiones");
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasColumnName("id_version");
            e.Property(v => v.ProyectoId).HasColumnName("id_proyecto");
            e.Property(v => v.NumeroVersion).HasColumnName("nombre_version");
            e.Property(v => v.Descripcion).HasColumnName("descripcion");
            e.Property(v => v.FechaVersion).HasColumnName("fecha_version");
            e.Property(v => v.Estado).HasColumnName("estado_version");
        });

        // ── REGLAS_EVALUACION ────────────────────────────────────
        modelBuilder.Entity<ReglaEvaluacion>(e =>
        {
            e.ToTable("reglas_evaluacion");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id_regla");
            e.Property(r => r.Nombre).HasColumnName("nombre_regla");
            e.Property(r => r.Descripcion).HasColumnName("descripcion");
            e.Property(r => r.Criterio).HasColumnName("criterio");
            e.Property(r => r.Umbral).HasColumnName("umbral");
            e.Property(r => r.Estado).HasColumnName("estado");
            e.Property(r => r.FechaCreacion).HasColumnName("fecha_creacion");
            e.Property(r => r.UsuarioCreacionId).HasColumnName("id_usuario_creacion");
        });

        // ── RESULTADOS_PRUEBA ────────────────────────────────────
        modelBuilder.Entity<ResultadoPrueba>(e =>
        {
            e.ToTable("resultados_prueba");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id_resultado");
            e.Property(r => r.VersionId).HasColumnName("id_version");
            e.Property(r => r.UsuarioCargaId).HasColumnName("id_usuario_carga");
            e.Property(r => r.NombreArchivo).HasColumnName("nombre_archivo");
            e.Property(r => r.FormatoArchivo).HasColumnName("formato_archivo");
            e.Property(r => r.RutaArchivo).HasColumnName("ruta_archivo");
            e.Property(r => r.FechaCarga).HasColumnName("fecha_carga");
            e.Property(r => r.EstadoValidacion).HasColumnName("estado_validacion");
            e.Property(r => r.Observaciones).HasColumnName("observaciones");
        });

        // ── METRICAS ─────────────────────────────────────────────
        modelBuilder.Entity<Metrica>(e =>
        {
            e.ToTable("metricas");
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).HasColumnName("id_metrica");
            e.Property(m => m.ResultadoId).HasColumnName("id_resultado");
            e.Property(m => m.NombreMetrica).HasColumnName("nombre_metrica");
            e.Property(m => m.ValorMetrica).HasColumnName("valor_metrica");
            e.Property(m => m.Unidad).HasColumnName("unidad");
            e.Property(m => m.FechaCalculo).HasColumnName("fecha_calculo");
        });

        // ── EVALUACIONES ─────────────────────────────────────────
        modelBuilder.Entity<Evaluacion>(e =>
        {
            e.ToTable("evaluaciones");
            e.HasKey(ev => ev.Id);
            e.Property(ev => ev.Id).HasColumnName("id_evaluacion");
            e.Property(ev => ev.ResultadoId).HasColumnName("id_resultado");
            e.Property(ev => ev.FechaEvaluacion).HasColumnName("fecha_evaluacion");
            e.Property(ev => ev.EstadoEvaluacion).HasColumnName("estado_evaluacion");
            e.Property(ev => ev.ResumenEvaluacion).HasColumnName("resumen_evaluacion");
        });

        // ── EVALUACION_REGLA ─────────────────────────────────────
        modelBuilder.Entity<EvaluacionRegla>(e =>
        {
            e.ToTable("evaluacion_regla");
            e.HasKey(er => er.Id);
            e.Property(er => er.Id).HasColumnName("id_evaluacion_regla");
            e.Property(er => er.EvaluacionId).HasColumnName("id_evaluacion");
            e.Property(er => er.ReglaId).HasColumnName("id_regla");
            e.Property(er => er.ResultadoRegla).HasColumnName("resultado_regla");
            e.Property(er => er.Observacion).HasColumnName("observacion");
        });

        // ── RECOMENDACIONES ──────────────────────────────────────
        modelBuilder.Entity<Recomendacion>(e =>
        {
            e.ToTable("recomendaciones");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id_recomendacion");
            e.Property(r => r.EvaluacionId).HasColumnName("id_evaluacion");
            e.Property(r => r.TipoRecomendacion).HasColumnName("tipo_recomendacion");
            e.Property(r => r.Justificacion).HasColumnName("justificacion");
            e.Property(r => r.FechaGeneracion).HasColumnName("fecha_generacion");
        });

        // ── DECISIONES_DESPLIEGUE ────────────────────────────────
        modelBuilder.Entity<DecisionDespliegue>(e =>
        {
            e.ToTable("decisiones_despliegue");
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasColumnName("id_decision");
            e.Property(d => d.RecomendacionId).HasColumnName("id_recomendacion");
            e.Property(d => d.UsuarioDecisorId).HasColumnName("id_usuario_decisor");
            e.Property(d => d.DecisionFinal).HasColumnName("decision_final");
            e.Property(d => d.Comentario).HasColumnName("comentario");
            e.Property(d => d.FechaDecision).HasColumnName("fecha_decision");
        });

        // ── AUDITORIA ────────────────────────────────────────────
        modelBuilder.Entity<Auditoria>(e =>
        {
            e.ToTable("auditoria");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id_auditoria");
            e.Property(a => a.UsuarioId).HasColumnName("id_usuario");
            e.Property(a => a.EntidadAfectada).HasColumnName("entidad_afectada");
            e.Property(a => a.IdRegistroAfectado).HasColumnName("id_registro_afectado");
            e.Property(a => a.Accion).HasColumnName("accion");
            e.Property(a => a.Detalle).HasColumnName("detalle");
            e.Property(a => a.FechaEvento).HasColumnName("fecha_evento");
            e.Property(a => a.IpOrigen).HasColumnName("ip_origen");
        });
    }
}
