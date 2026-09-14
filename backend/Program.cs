using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Text;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Services;
using DecisionSupportAPI.Auth;

// Fix: Npgsql requiere DateTimeKind.Utc para timestamptz.
// Habilitar comportamiento legacy para que acepte Kind=Unspecified (leído de PG).
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// JWT configuration — sin fallback inseguro: si falta o es débil, el sistema
// no debe arrancar (evita firmar tokens con una clave adivinable, y evita
// que este fallback difiera silenciosamente del usado en AuthenticationService).
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key debe estar configurado (appsettings o variable de entorno) con al menos 32 caracteres.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Reconstruye rol/permiso desde la base en cada request (no desde el JWT
// congelado): revocar un permiso, cambiar un rol o desactivar un usuario se
// aplica de inmediato, sin esperar a que ese usuario vuelva a iniciar sesión.
builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, DbClaimsTransformation>();

// Autorización basada en permisos (RF14 / RBAC): cada política exige el claim
// "permission" correspondiente, emitido en el JWT según rol_permiso (seed.sql).
builder.Services.AddAuthorization(options =>
{
    string[] permisos =
    {
        "gestionar_usuarios", "ver_usuarios",
        "gestionar_proyectos", "ver_proyectos",
        "cargar_resultados", "ver_resultados",
        "ejecutar_evaluacion", "ver_evaluacion", "gestionar_reglas",
        "registrar_decision", "ver_decisiones",
        "ver_auditoria",
    };
    foreach (var permiso in permisos)
        options.AddPolicy(permiso, policy => policy.RequireClaim("permission", permiso));
});

// Services registration
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IMetricsCalculationService, MetricsCalculationService>();
builder.Services.AddScoped<IRecommendationEngine, RecommendationEngine>();
builder.Services.AddScoped<IIngestaResultadosService, IngestaResultadosService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IProyectoAccesoService, ProyectoAccesoService>();
builder.Services.AddHttpContextAccessor();

// RNF12: respaldo periódico de la BD. Se registra como singleton (además de
// hosted service) para que BackupController pueda inyectarlo directamente y
// disparar un respaldo manual bajo demanda, reusando la misma lógica.
builder.Services.AddSingleton<DatabaseBackupService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DatabaseBackupService>());

// Módulo de suscripciones
builder.Services.AddScoped<ISuscripcionService, SuscripcionService>();

// AdamsPay
builder.Services.AddScoped<IAdamsPayService, AdamsPayService>();
builder.Services.AddHttpClient("AdamsPay", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// PayPal
builder.Services.AddScoped<IPayPalService, PayPalService>();
builder.Services.AddHttpClient("PayPal", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddHttpClient("ExchangeRate", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddMemoryCache();

// Email + PDF receipt
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<IReceiptService, ReceiptService>();
builder.Services.AddSingleton<IActaPdfService, ActaPdfService>();
builder.Services.AddSingleton<IReporteExportService, ReporteExportService>();

// CORS configuration — lista explícita de orígenes en vez de AllowAnyOrigin.
// Configurable vía Cors:AllowedOrigins (appsettings.Production.json o variable
// de entorno) para agregar el dominio real de despliegue sin tocar código.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://localhost:5173" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Rate limiting: máximo 8 intentos de login por IP cada 5 minutos, para
// mitigar fuerza bruta de contraseñas (no había ninguna protección — se
// pudo confirmar 15 intentos fallidos seguidos sin bloqueo ni demora).
// Particionado por IP (RemoteIpAddress): AddFixedWindowLimiter sin partición
// crea un único cupo GLOBAL compartido por todos los clientes, lo que
// permitiría que un atacante agote los intentos de login de todos los
// usuarios legítimos (DoS) en vez de limitar solo su propia IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Manejador global de excepciones: sin esto, una violación de CHECK constraint
// de Postgres (enum inválido) o de longitud de columna (VARCHAR excedido) —
// ninguna de las dos validada antes en varios controllers — se propagaba como
// un 500 vacío en vez de un 400 con mensaje claro.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Excepción no controlada en {Path}", context.Request.Path);

        context.Response.ContentType = "application/json";

        // EF Core envuelve el error real de Postgres en DbUpdateException.InnerException,
        // nunca lo lanza directamente.
        var pgEx = (ex as PostgresException) ?? (ex?.InnerException as PostgresException);
        var esViolacionDeDatos = pgEx != null
            && (pgEx.SqlState == PostgresErrorCodes.CheckViolation
                || pgEx.SqlState == PostgresErrorCodes.StringDataRightTruncation
                || pgEx.SqlState == PostgresErrorCodes.NotNullViolation
                || pgEx.SqlState == PostgresErrorCodes.UniqueViolation
                || pgEx.SqlState == PostgresErrorCodes.ForeignKeyViolation);

        if (esViolacionDeDatos)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Los datos enviados no son válidos." });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(app.Environment.IsDevelopment()
            ? new { message = ex?.Message ?? "Error interno." }
            : new { message = "Ocurrió un error interno." });
    });
});

app.UseHttpsRedirection();
app.UseCors("AllowReactApp");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
