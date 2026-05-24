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

builder.WebHost.UseUrls(
    "http://0.0.0.0:5158"
);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(
    options =>
    {
        options.UseInMemoryDatabase(
            "DriverAIDB"
        );
    }
);

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

app.Run();