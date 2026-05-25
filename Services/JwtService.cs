using DriverAI.API.Models.Entities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DriverAI.API.Services;

public class JwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var jwtKey =
            _configuration["Jwt:Key"]
            ?? Environment.GetEnvironmentVariable("JWT_KEY")
            ?? "DriverAI_SUPER_SECRET_KEY_2026_ULTRA_SECURE_123456789";

        var jwtIssuer =
            _configuration["Jwt:Issuer"]
            ?? Environment.GetEnvironmentVariable("JWT_ISSUER")
            ?? "DriverAI";

        var jwtAudience =
            _configuration["Jwt:Audience"]
            ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE")
            ?? "DriverAIUsers";

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)
        );

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256
        );

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}