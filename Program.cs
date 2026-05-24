using DriverAI.API.Config;
using DriverAI.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// CONFIGURACIÓN BÁSICA
// ==========================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==========================================
// CONFIGURACIÓN DE BASE DE DATOS
// ==========================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
}

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("⚠️ ADVERTENCIA: No se encontró cadena de conexión. Usando base de datos en memoria.");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("DriverAIDB"));
}
else
{
    Console.WriteLine("✅ Usando PostgreSQL con Npgsql 8.0.4");
    
    // Limpiar la cadena de conexión de parámetros problemáticos
    connectionString = connectionString
        .Replace("SSL Mode=Require", "")
        .Replace("Trust Server Certificate=true", "")
        .Replace(";;", ";")
        .Trim(';', ' ');
    
    // Agregar SSL Mode=Require al final si no existe
    if (!connectionString.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase))
    {
        connectionString += ";SSL Mode=Require";
    }
    
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(3);
            npgsqlOptions.CommandTimeout(30);
        }));
}

// ==========================================
// JWT CONFIGURACIÓN
// ==========================================
builder.Services.AddScoped<JwtService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "DriverAI_SUPER_SECRET_KEY_2026_ULTRA_SECURE_123456789";
var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "DriverAI",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "DriverAIUsers",
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

// ==========================================
// SERVICIOS PERSONALIZADOS
// ==========================================
builder.Services.AddScoped<RecopeService>();      // ← Servicio para consumir API de Recope
builder.Services.AddHttpClient<RecopeService>();  // ← HttpClient para RecopeService

// ==========================================
// CORS CONFIGURACIÓN
// ==========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ==========================================
// MIDDLEWARE PIPELINE
// ==========================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ==========================================
// APLICAR MIGRACIONES AUTOMÁTICAMENTE
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var isPostgreSQL = dbContext.Database.ProviderName?.Contains("PostgreSQL") == true;
    
    if (isPostgreSQL)
    {
        try
        {
            Console.WriteLine("🔄 Verificando/ aplicando migraciones pendientes...");
            
            // Verificar si la base de datos existe y las migraciones están aplicadas
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            var pendingCount = pendingMigrations.Count();
            
            if (pendingCount > 0)
            {
                Console.WriteLine($"📦 Se encontraron {pendingCount} migraciones pendientes. Aplicando...");
                await dbContext.Database.MigrateAsync();
                Console.WriteLine("✅ Migraciones aplicadas correctamente");
            }
            else
            {
                Console.WriteLine("✅ No hay migraciones pendientes. La base de datos está actualizada.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error al aplicar migraciones: {ex.Message}");
            
            // Mostrar más detalles del error
            if (ex.InnerException != null)
            {
                Console.WriteLine($"📎 Detalle interno: {ex.InnerException.Message}");
            }
        }
    }
    else
    {
        Console.WriteLine("ℹ️ Usando base de datos en memoria. No se aplican migraciones.");
    }
}

app.Run();