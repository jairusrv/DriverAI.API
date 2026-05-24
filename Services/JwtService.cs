using DriverAI.API.Models;

using Microsoft.IdentityModel.Tokens;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DriverAI.API.Services;

public class JwtService
{
    private readonly IConfiguration
        _configuration;

    public JwtService(
        IConfiguration configuration
    )
    {
        _configuration = configuration;
    }

    public string GenerateToken(
        User user
    )
    {
        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _configuration["Jwt:Key"]!
                )
            );

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

        var claims =
            new[]
            {
                new Claim(
                    ClaimTypes.Name,
                    user.Email
                )
            };

        var token =
            new JwtSecurityToken(
                issuer:
                    _configuration["Jwt:Issuer"],

                audience:
                    _configuration["Jwt:Audience"],

                claims: claims,

                expires:
                    DateTime.UtcNow
                        .AddDays(7),

                signingCredentials:
                    credentials
            );

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}