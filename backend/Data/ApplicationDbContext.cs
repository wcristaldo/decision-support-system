using Microsoft.EntityFrameworkCore;
using DecisionSupportAPI.Models;

namespace DecisionSupportAPI.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Rol> Roles { get; set; }
    public DbSet<UsuarioRol> UsuariosRoles { get; set; }
    public DbSet<Permiso> Permisos { get; set; }
    public DbSet<RolPermiso> RolesPermisos { get; set; }
    public DbSet<Proyecto> Proyectos { get; set; }
    public DbSet<Models.Version> Versiones { get; set; }
    public DbSet<ResultadoPrueba> ResultadosPrueba { get; set; }
    public DbSet<Metrica> Metricas { get; set; }
    public DbSet<Evaluacion> Evaluaciones { get; set; }
    public DbSet<ReglaEvaluacion> ReglasEvaluacion { get; set; }
    public DbSet<Recomendacion> Recomendaciones { get; set; }
    public DbSet<DecisionDespliegue> DecisionesDespliegue { get; set; }
    public DbSet<Auditoria> Auditoria { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configurar PostgreSQL para usar snake_case en columnas
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                var columnName = property.Name;
                columnName = string.Concat(columnName.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString().ToLower() : x.ToString().ToLower()));
                property.SetColumnName(columnName);
            }
        }

        // Mapear nombres de tablas a minúsculas
        modelBuilder.Entity<Usuario>().ToTable("usuarios");
        modelBuilder.Entity<Rol>().ToTable("roles");
        modelBuilder.Entity<UsuarioRol>().ToTable("usuarios_roles");
        modelBuilder.Entity<Permiso>().ToTable("permisos");
        modelBuilder.Entity<RolPermiso>().ToTable("roles_permisos");
        modelBuilder.Entity<Proyecto>().ToTable("proyectos");
        modelBuilder.Entity<Models.Version>().ToTable("versiones");
        modelBuilder.Entity<ResultadoPrueba>().ToTable("resultados_prueba");
        modelBuilder.Entity<Metrica>().ToTable("metricas");
        modelBuilder.Entity<Evaluacion>().ToTable("evaluaciones");
        modelBuilder.Entity<ReglaEvaluacion>().ToTable("reglas_evaluacion");
        modelBuilder.Entity<Recomendacion>().ToTable("recomendaciones");
        modelBuilder.Entity<DecisionDespliegue>().ToTable("decisiones_despliegue");
        modelBuilder.Entity<Auditoria>().ToTable("auditoria");

        // Configuraciones de claves primarias y relaciones
        modelBuilder.Entity<UsuarioRol>()
            .HasKey(ur => new { ur.UsuarioId, ur.RolId });

        modelBuilder.Entity<RolPermiso>()
            .HasKey(rp => new { rp.RolId, rp.PermisoId });

        // Índices
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Models.Version>()
            .HasIndex(v => new { v.ProyectoId, v.NumeroVersion })
            .IsUnique();

        modelBuilder.Entity<Rol>()
            .HasIndex(r => r.Nombre)
            .IsUnique();

        modelBuilder.Entity<Permiso>()
            .HasIndex(p => p.Nombre)
            .IsUnique();

        // Datos iniciales
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Roles
        modelBuilder.Entity<Rol>().HasData(
            new Rol { Id = 1, Nombre = "Administrador", Descripcion = "Acceso total al sistema" },
            new Rol { Id = 2, Nombre = "Analista", Descripcion = "Puede analizar resultados y generar recomendaciones" },
            new Rol { Id = 3, Nombre = "Gerente", Descripcion = "Puede tomar decisiones de despliegue" },
            new Rol { Id = 4, Nombre = "Visualizador", Descripcion = "Acceso de solo lectura" }
        );

        // Permisos
        modelBuilder.Entity<Permiso>().HasData(
            new Permiso { Id = 1, Nombre = "crear_proyecto", Descripcion = "Crear nuevos proyectos" },
            new Permiso { Id = 2, Nombre = "editar_proyecto", Descripcion = "Editar proyectos existentes" },
            new Permiso { Id = 3, Nombre = "eliminar_proyecto", Descripcion = "Eliminar proyectos" },
            new Permiso { Id = 4, Nombre = "cargar_pruebas", Descripcion = "Cargar resultados de pruebas" },
            new Permiso { Id = 5, Nombre = "generar_recomendaciones", Descripcion = "Generar recomendaciones automáticas" },
            new Permiso { Id = 6, Nombre = "tomar_decision_despliegue", Descripcion = "Tomar decisiones sobre despliegues" },
            new Permiso { Id = 7, Nombre = "ver_auditoria", Descripcion = "Ver registro de auditoría" },
            new Permiso { Id = 8, Nombre = "gestionar_usuarios", Descripcion = "Crear y modificar usuarios" }
        );

        // Relaciones Rol-Permiso para Admin (todos los permisos)
        for (int i = 1; i <= 8; i++)
        {
            modelBuilder.Entity<RolPermiso>().HasData(
                new RolPermiso { RolId = 1, PermisoId = i }
            );
        }

        // Relaciones Rol-Permiso para Analista
        modelBuilder.Entity<RolPermiso>().HasData(
            new RolPermiso { RolId = 2, PermisoId = 4 },
            new RolPermiso { RolId = 2, PermisoId = 5 }
        );

        // Relaciones Rol-Permiso para Gerente
        modelBuilder.Entity<RolPermiso>().HasData(
            new RolPermiso { RolId = 3, PermisoId = 5 },
            new RolPermiso { RolId = 3, PermisoId = 6 },
            new RolPermiso { RolId = 3, PermisoId = 7 }
        );
    }
}
