using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using DecisionSupportAPI.Data;
using DecisionSupportAPI.Services;

// Fix: Npgsql requiere DateTimeKind.Utc para timestamptz.
// Habilitar comportamiento legacy para que acepte Kind=Unspecified (leído de PG).
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ── Railway: PORT dinámico ────────────────────────────────────────────────────
// Railway inyecta $PORT en cada deploy. Si no está definido, usamos 5000 (local).
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ── Railway: conexión PostgreSQL ──────────────────────────────────────────────
// Railway inyecta PGHOST, PGPORT, PGDATABASE, PGUSER, PGPASSWORD automáticamente
// cuando el servicio tiene referencia al addon de Postgres.
// Usamos esas variables individuales en vez de parsear DATABASE_URL (más seguro
// con passwords que contienen caracteres especiales).
var pgHost = Environment.GetEnvironmentVariable("PGHOST");
if (!string.IsNullOrEmpty(pgHost))
{
    var connBuilder = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host               = pgHost,
        Port               = int.Parse(Environment.GetEnvironmentVariable("PGPORT") ?? "5432"),
        Database           = Environment.GetEnvironmentVariable("PGDATABASE") ?? "railway",
        Username           = Environment.GetEnvironmentVariable("PGUSER")     ?? "postgres",
        Password           = Environment.GetEnvironmentVariable("PGPASSWORD") ?? "",
        SslMode            = Npgsql.SslMode.Require,
        TrustServerCertificate = true
    };
    builder.Configuration["ConnectionStrings:DefaultConnection"] = connBuilder.ConnectionString;
}
else
{
    // Fallback: parsear DATABASE_URL si las variables PG* no están disponibles
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        try
        {
            var uri      = new Uri(databaseUrl);
            var userParts = uri.UserInfo.Split(':', 2);
            var connBuilder = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host               = uri.Host,
                Port               = uri.Port > 0 ? uri.Port : 5432,
                Database           = uri.AbsolutePath.TrimStart('/'),
                Username           = Uri.UnescapeDataString(userParts[0]),
                Password           = userParts.Length > 1 ? Uri.UnescapeDataString(userParts[1]) : "",
                SslMode            = Npgsql.SslMode.Require,
                TrustServerCertificate = true
            };
            builder.Configuration["ConnectionStrings:DefaultConnection"] = connBuilder.ConnectionString;
        }
        catch { /* Usar la cadena de appsettings si el parsing falla */ }
    }
}

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// JWT configuration
var jwtKey = builder.Configuration["Jwt:Key"] ?? "default-key";
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

// Services registration
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IMetricsCalculationService, MetricsCalculationService>();
builder.Services.AddScoped<IRecommendationEngine, RecommendationEngine>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddHttpContextAccessor();

// Módulo de suscripciones + Pagopar
builder.Services.AddScoped<ISuscripcionService, SuscripcionService>();
builder.Services.AddScoped<IPagoparService, PagoparService>();
builder.Services.AddHttpClient("Pagopar", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── Health check para Railway ─────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();

app.UseHttpsRedirection();
app.UseCors("AllowReactApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
