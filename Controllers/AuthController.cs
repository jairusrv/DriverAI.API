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
    
    public AuthController(
        AppDbContext context,
        JwtService jwtService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var errors = new List<string>();
        
        if (!IsValidPhoneFormat(request.PhoneNumber))
        {
            errors.Add("El número de teléfono debe tener exactamente 8 dígitos (ej: 88888888)");
            return BadRequest(new { success = false, message = "Formato de teléfono inválido.", errors });
        }
        
        var fullPhoneNumber = $"+506{request.PhoneNumber}";
        
        // Verificar si el IMEI ya existe
        var existingUserByImei = await _context.Users.FirstOrDefaultAsync(u => u.Imei == request.Imei);
        if (existingUserByImei != null)
        {
            var hasAccess = existingUserByImei.HasAccess();
            return Conflict(new
            {
                success = false,
                message = "Este dispositivo ya está registrado.",
                errors = new List<string> { "device_exists", hasAccess ? "access_active" : "subscription_required" },
                data = new
                {
                    remainingTrialDays = existingUserByImei.GetRemainingTrialDays(),
                    subscriptionExpiryDate = existingUserByImei.SubscriptionExpiryDate
                }
            });
        }
        
        // Validaciones de unicidad
        var existingPhone = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
        if (existingPhone != null) errors.Add("El número de teléfono ya está registrado.");
        
        var existingEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingEmail != null) errors.Add("El email ya está registrado con otra cuenta.");
        
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (existingUser != null) errors.Add("El nombre de usuario ya está en uso.");
        
        if (errors.Any())
            return BadRequest(new { success = false, message = "No se pudo completar el registro.", errors });
        
        // Hash de contraseña
        using var sha256 = SHA256.Create();
        var passwordHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(request.Password)));
        
        var trialEndDate = DateTime.UtcNow.AddDays(7);
        
        var user = new User
        {
            Imei = request.Imei,
            PhoneNumber = fullPhoneNumber,
            Email = request.Email,
            Username = request.Username,
            PasswordHash = passwordHash,
            IsEmailVerified = true,
            IsPhoneVerified = true,
            CreatedAt = DateTime.UtcNow,
            TrialEndDate = trialEndDate,
            IsSubscriptionActive = false,
            SubscriptionExpiryDate = null
        };
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email);
        var displayPhone = user.PhoneNumber.Substring(4);
        
        return Ok(new
        {
            success = true,
            message = "Registro exitoso. Ya puedes usar DriverAI.",
            data = new
            {
                token,
                user = new { user.Id, phoneNumber = displayPhone, user.Email, user.Username },
                subscription = new
                {
                    trialEndDate,
                    remainingTrialDays = user.GetRemainingTrialDays(),
                    message = $"¡Bienvenido! Disfruta de {user.GetRemainingTrialDays()} días gratis."
                }
            }
        });
    }

    [HttpGet("subscription-status/{phoneNumber}")]
public async Task<IActionResult> GetSubscriptionStatus(string phoneNumber)
{
    if (!IsValidPhoneFormat(phoneNumber))
        return BadRequest(new { success = false, message = "Teléfono debe tener 8 dígitos" });

    var fullPhoneNumber = $"+506{phoneNumber}";
    var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
    if (user == null)
        return NotFound(new { success = false, message = "Usuario no encontrado" });

    var hasAccess = user.HasAccess();
    var result = new
    {
        success = true,
        data = new
        {
            hasAccess,
            isInTrial = user.TrialEndDate > DateTime.UtcNow,
            remainingTrialDays = user.GetRemainingTrialDays(),
            isSubscriptionActive = user.IsSubscriptionActive && user.SubscriptionExpiryDate > DateTime.UtcNow,
            subscriptionExpiryDate = user.SubscriptionExpiryDate,
            message = hasAccess ? "Acceso activo" : "Suscripción requerida"
        }
    };
    return Ok(result);
}
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!IsValidPhoneFormat(request.PhoneNumber))
            return Unauthorized(new { success = false, message = "El teléfono debe tener exactamente 8 dígitos" });
        
        var fullPhoneNumber = $"+506{request.PhoneNumber}";
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
        
        if (user == null)
            return Unauthorized(new { success = false, message = "Credenciales inválidas." });
        
        using var sha256 = SHA256.Create();
        var passwordHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(request.Password)));
        if (user.PasswordHash != passwordHash)
            return Unauthorized(new { success = false, message = "Credenciales inválidas." });
        
        var hasAccess = user.HasAccess();
        if (!hasAccess)
            return Unauthorized(new { success = false, message = "Tu período de prueba ha expirado. Activa tu suscripción.", errors = new List<string> { "subscription_expired" } });
        
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email);
        var displayPhone = user.PhoneNumber.Substring(4);
        
        return Ok(new
        {
            success = true,
            message = "Login exitoso.",
            data = new
            {
                token,
                user = new { user.Id, phoneNumber = displayPhone, user.Email, user.Username },
                subscription = new
                {
                    hasAccess,
                    isInTrial = user.TrialEndDate > DateTime.UtcNow,
                    remainingTrialDays = user.GetRemainingTrialDays(),
                    isSubscriptionActive = user.IsSubscriptionActive && user.SubscriptionExpiryDate > DateTime.UtcNow,
                    subscriptionExpiryDate = user.SubscriptionExpiryDate,
                    message = user.TrialEndDate > DateTime.UtcNow ? $"Te quedan {user.GetRemainingTrialDays()} días de prueba." : "Suscripción activa."
                }
            }
        });
    }
    
    private bool IsValidPhoneFormat(string phoneNumber)
    {
        return !string.IsNullOrEmpty(phoneNumber) && phoneNumber.Length == 8 && phoneNumber.All(char.IsDigit);
    }
}