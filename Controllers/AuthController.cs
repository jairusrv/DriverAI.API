using System.Security.Cryptography;
using System.Text;
using DriverAI.API.Config;
using DriverAI.API.Models;
using DriverAI.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DriverAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly JwtService _jwtService;
    private readonly ILogger<AuthController> _logger;
    
    public AuthController(AppDbContext context, JwtService jwtService, ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
    }
    
    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
    
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Validar si el usuario ya existe
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);
        
        if (existingUser != null)
        {
            return BadRequest(new { message = "El email ya está registrado" });
        }
        
        // Crear hash de la contraseña
        using var sha256 = SHA256.Create();
        var passwordHash = Convert.ToBase64String(
            sha256.ComputeHash(Encoding.UTF8.GetBytes(request.Password))
        );
        
        var user = new User
        {
            Email = request.Email,
            Username = request.Username,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        // Generar token
        var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email);
        
        return Ok(new { 
            message = "Usuario registrado exitosamente",
            token,
            user = new { user.Id, user.Email, user.Username }
        });
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Buscar usuario por email
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);
        
        if (user == null)
        {
            return Unauthorized(new { message = "Credenciales inválidas" });
        }
        
        // Verificar contraseña
        using var sha256 = SHA256.Create();
        var passwordHash = Convert.ToBase64String(
            sha256.ComputeHash(Encoding.UTF8.GetBytes(request.Password))
        );
        
        if (user.PasswordHash != passwordHash)
        {
            return Unauthorized(new { message = "Credenciales inválidas" });
        }
        
        // Generar token
        var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email);
        
        return Ok(new { 
            message = "Login exitoso",
            token,
            user = new { user.Id, user.Email, user.Username }
        });
    }
}