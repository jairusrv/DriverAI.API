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
    private readonly EmailService _emailService;
    private readonly SmsService _smsService;
    private readonly ILogger<AuthController> _logger;
    
    public AuthController(
        AppDbContext context,
        JwtService jwtService,
        EmailService emailService,
        SmsService smsService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _emailService = emailService;
        _smsService = smsService;
        _logger = logger;
    }
    
    // ==========================================
    // 1. REGISTRO (auto-verifica email y teléfono)
    // ==========================================
    [HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterRequest request)
{
    var errors = new List<string>();

    // Validar formato del teléfono (8 dígitos)
    if (!IsValidPhoneFormat(request.PhoneNumber))
    {
        errors.Add("El número de teléfono debe tener exactamente 8 dígitos (ej: 88888888)");
        return BadRequest(ApiResponse<object>.Error("Formato de teléfono inválido.", errors));
    }

    var fullPhoneNumber = $"+506{request.PhoneNumber}";

    // Verificar si el IMEI ya existe
    var existingUserByImei = await _context.Users.FirstOrDefaultAsync(u => u.Imei == request.Imei);
    if (existingUserByImei != null)
    {
        // El dispositivo ya está registrado. Verificar si aún tiene acceso (prueba o suscripción activa)
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

    // Validar teléfono único
    var existingPhone = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
    if (existingPhone != null) errors.Add("El número de teléfono ya está registrado.");

    // Validar email único
    var existingEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
    if (existingEmail != null) errors.Add("El email ya está registrado con otra cuenta.");

    // Validar usuario único
    var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
    if (existingUser != null) errors.Add("El nombre de usuario ya está en uso.");

    if (errors.Any())
        return BadRequest(ApiResponse<object>.Error("No se pudo completar el registro.", errors));

    // Crear hash de contraseña
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
        // Auto-verificar ambos
        IsEmailVerified = true,
        IsPhoneVerified = true,
        CreatedAt = DateTime.UtcNow,
        TrialEndDate = trialEndDate,
        IsSubscriptionActive = false,
        SubscriptionExpiryDate = null
    };

    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    // Generar token JWT
    var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email);
    var displayPhone = user.PhoneNumber.Substring(4);

    return Ok(ApiResponse<object>.Ok(
        new
        {
            token,
            user = new { user.Id, phoneNumber = displayPhone, user.Email, user.Username },
            subscription = new
            {
                trialEndDate,
                remainingTrialDays = user.GetRemainingTrialDays(),
                message = $"¡Bienvenido! Disfruta de {user.GetRemainingTrialDays()} días gratis."
            }
        },
        "Registro exitoso. Ya puedes usar DriverAI."
    ));
}
    
    // Resto de métodos (verify-email, verify-code, resend, login, etc.)
    // Puedes mantenerlos o eliminarlos, pero no son necesarios si auto-verificas.
    // Para mantener la compatibilidad, déjalos pero devuelven error o éxito rápido.
    
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        return Ok(ApiResponse<object>.Ok(null, "Email ya verificado (modo prueba)."));
    }
    
    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request)
    {
        return Ok(ApiResponse<object>.Ok(null, "Teléfono ya verificado (modo prueba)."));
    }
    
    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendCode([FromBody] ResendCodeRequest request)
    {
        return Ok(ApiResponse<object>.Ok(null, "No es necesario reenviar código."));
    }
    
    [HttpPost("resend-email-code")]
    public async Task<IActionResult> ResendEmailCode([FromBody] ResendEmailCodeRequest request)
    {
        return Ok(ApiResponse<object>.Ok(null, "No es necesario reenviar código."));
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!IsValidPhoneFormat(request.PhoneNumber))
            return Unauthorized(ApiResponse<object>.Error("El teléfono debe tener exactamente 8 dígitos"));
        
        var fullPhoneNumber = $"+506{request.PhoneNumber}";
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
        
        if (user == null)
            return Unauthorized(ApiResponse<object>.Error("Credenciales inválidas."));
        
        using var sha256 = SHA256.Create();
        var passwordHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(request.Password)));
        if (user.PasswordHash != passwordHash)
            return Unauthorized(ApiResponse<object>.Error("Credenciales inválidas."));
        
        var hasAccess = user.HasAccess();
        if (!hasAccess)
            return Unauthorized(ApiResponse<object>.Error("Tu período de prueba ha expirado. Activa tu suscripción.", new List<string> { "subscription_expired" }));
        
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email);
        var displayPhone = user.PhoneNumber.Substring(4);
        
        var subscriptionInfo = new
        {
            hasAccess = true,
            isInTrial = user.TrialEndDate > DateTime.UtcNow,
            remainingTrialDays = user.GetRemainingTrialDays(),
            isSubscriptionActive = user.IsSubscriptionActive && user.SubscriptionExpiryDate > DateTime.UtcNow,
            subscriptionExpiryDate = user.SubscriptionExpiryDate,
            message = user.TrialEndDate > DateTime.UtcNow ? $"Te quedan {user.GetRemainingTrialDays()} días de prueba." : "Suscripción activa."
        };
        
        return Ok(ApiResponse<object>.Ok(
            new { token, user = new { user.Id, phoneNumber = displayPhone, user.Email, user.Username }, subscription = subscriptionInfo },
            "Login exitoso."
        ));
    }
    
    // Métodos auxiliares
    private bool IsValidPhoneFormat(string phoneNumber)
    {
        return !string.IsNullOrEmpty(phoneNumber) && phoneNumber.Length == 8 && phoneNumber.All(char.IsDigit);
    }
    
    private string GenerateRandomCode()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }
}