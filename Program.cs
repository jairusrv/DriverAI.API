//using DriverAI.API.Auth;
using DriverAI.API.Config;
//using DriverAI.API.Middleware;
using DriverAI.API.Services;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using System.Text;

var builder =
    WebApplication.CreateBuilder(args);

// 📌 Eliminar UseUrls fijo - Render usará el puerto 10000 por variable de entorno
// builder.WebHost.UseUrls("http://0.0.0.0:5158");

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

// ==========================================
// 🔧 CONFIGURACIÓN DE BASE DE DATOS POSTGRESQL
// ==========================================
// Obtener cadena de conexión desde variable de entorno o appsettings.json
// Render inyectará ConnectionStrings__DefaultConnection automáticamente
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Si no existe, intentar obtener directamente desde variable de entorno (fallback)
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
}

// Validar que la cadena de conexión existe
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("⚠️ ADVERTENCIA: No se encontró cadena de conexión. Usando base de datos en memoria como fallback.");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("DriverAIDB"));
}
else
{
    Console.WriteLine("✅ Usando PostgreSQL en Render");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}

builder.Services.AddScoped<JwtService>();

//builder.Services.AddScoped<AuthService>();

//builder.Services.AddScoped<AIService>();

var jwtKey =
    builder.Configuration["Jwt:Key"]
    ?? "DriverAI_SUPER_SECRET_KEY_2026_ULTRA_SECURE_123456789";

var key =
    Encoding.UTF8.GetBytes(jwtKey);

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme
    )
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,

                ValidateAudience = true,

                ValidateLifetime = true,

                ValidateIssuerSigningKey = true,

                ValidIssuer =
                    builder.Configuration["Jwt:Issuer"]
                    ?? "DriverAI",

                ValidAudience =
                    builder.Configuration["Jwt:Audience"]
                    ?? "DriverAIUsers",

                IssuerSigningKey =
                    new SymmetricSecurityKey(key)
            };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowAll",
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

var app = builder.Build();

app.UseSwagger();

app.UseSwaggerUI();

app.UseCors("AllowAll");

app.UseAuthentication();

app.UseAuthorization();

//app.UseMiddleware<AuthMiddleware>();

app.MapControllers();

// ==========================================
// 🚀 APLICAR MIGRACIONES AUTOMÁTICAMENTE AL INICIAR
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // Verificar si estamos usando PostgreSQL (no en memoria)
    if (dbContext.Database.ProviderName?.Contains("PostgreSQL") == true)
    {
        try
        {
            Console.WriteLine("🔄 Aplicando migraciones pendientes...");
            dbContext.Database.Migrate();
            Console.WriteLine("✅ Migraciones aplicadas correctamente");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error al aplicar migraciones: {ex.Message}");
        }
    }
}

app.Run();