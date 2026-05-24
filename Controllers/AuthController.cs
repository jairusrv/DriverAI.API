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
    private readonly SmsService _smsService;
    private readonly ILogger<AuthController> _logger;
    
    public AuthController(
        AppDbContext context,
        JwtService jwtService,
        SmsService smsService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _smsService = smsService;
        _logger = logger;
    }
    
    // ==========================================
    // 1. REGISTRO
    // ==========================================
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var errors = new List<string>();
        
        // Validar formato de 8 dígitos
        if (!IsValidPhoneFormat(request.PhoneNumber))
        {
            errors.Add("El número de teléfono debe tener exactamente 8 dígitos (ej: 88888888)");
            return BadRequest(ApiResponse<object>.Error(
                "Formato de teléfono inválido.",
                errors));
        }
        
        // Agregar código de país +506
        var fullPhoneNumber = $"+506{request.PhoneNumber}";
        
        // Validar teléfono único (con +506)
        var existingPhone = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
        
        if (existingPhone != null)
        {
            errors.Add("El número de teléfono ya está registrado.");
        }
        
        // Validar email único
        var existingEmail = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);
        
        if (existingEmail != null)
        {
            errors.Add("El email ya está registrado con otra cuenta.");
        }
        
        // Validar usuario único
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);
        
        if (existingUser != null)
        {
            errors.Add("El nombre de usuario ya está en uso.");
        }
        
        if (errors.Any())
        {
            return BadRequest(ApiResponse<object>.Error(
                "No se pudo completar el registro.",
                errors));
        }
        
        // Generar código SMS
        var smsCode = GenerateRandomCode();
        
        // Crear hash de contraseña
        using var sha256 = SHA256.Create();
        var passwordHash = Convert.ToBase64String(
            sha256.ComputeHash(Encoding.UTF8.GetBytes(request.Password))
        );
        
        // Calcular fecha de fin de prueba (7 días gratis)
        var trialEndDate = DateTime.UtcNow.AddDays(7);
        
        // Crear usuario con número completo (+506)
        var user = new User
        {
            PhoneNumber = fullPhoneNumber,
            Email = request.Email,
            Username = request.Username,
            PasswordHash = passwordHash,
            IsPhoneVerified = false,
            SmsVerificationCode = smsCode,
            SmsVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow,
            TrialEndDate = trialEndDate,
            IsSubscriptionActive = false,
            SubscriptionExpiryDate = null
        };
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        // Enviar código SMS al número completo
        var smsSent = await _smsService.SendVerificationCodeAsync(fullPhoneNumber, smsCode);
        
        return Ok(ApiResponse<object>.Ok(
            new { 
                phoneNumber = request.PhoneNumber,
                countryCode = "+506",
                trialEndDate = trialEndDate,
                freeTrialDays = 7,
                requiresSmsVerification = true
            },
            smsSent 
                ? $"Te hemos enviado un código de verificación por SMS al número {request.PhoneNumber}. Tienes 7 días gratis a partir de la verificación."
                : "No se pudo enviar el SMS. Intenta nuevamente."
        ));
    }
    
    // ==========================================
    // 2. VERIFICAR CÓDIGO SMS
    // ==========================================
    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request)
    {
        // Validar formato de 8 dígitos
        if (!IsValidPhoneFormat(request.PhoneNumber))
        {
            return BadRequest(ApiResponse<object>.Error(
                "El teléfono debe tener exactamente 8 dígitos"));
        }
        
        // Agregar código de país +506 para buscar en BD
        var fullPhoneNumber = $"+506{request.PhoneNumber}";
        
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
        
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Error(
                "No se encontró un usuario con este número de teléfono."));
        }
        
        if (user.IsPhoneVerified)
        {
            return BadRequest(ApiResponse<object>.Error(
                "Este número de teléfono ya ha sido verificado."));
        }
        
        // Verificar código SMS
        if (user.SmsVerificationCode != request.Code)
        {
            return BadRequest(ApiResponse<object>.Error(
                "Código de verificación incorrecto."));
        }
        
        if (user.SmsVerificationCodeExpiry < DateTime.UtcNow)
        {
            return BadRequest(ApiResponse<object>.Error(
                "El código de verificación ha expirado. Solicita uno nuevo."));
        }
        
        // Marcar como verificado
        user.IsPhoneVerified = true;
        user.SmsVerificationCode = null;
        user.SmsVerificationCodeExpiry = null;
        
        await _context.SaveChangesAsync();
        
        // Generar token JWT
        var token = _jwtService.GenerateToken(user.Id.ToString(), user.PhoneNumber);
        var displayPhone = user.PhoneNumber.Substring(4);
        
        return Ok(ApiResponse<object>.Ok(
            new { 
                token,
                user = new { 
                    user.Id, 
                    phoneNumber = displayPhone,
                    user.Email, 
                    user.Username 
                },
                subscription = new
                {
                    trialEndDate = user.TrialEndDate,
                    remainingTrialDays = user.GetRemainingTrialDays(),
                    message = $"¡Bienvenido! Disfruta de {user.GetRemainingTrialDays()} días gratis. Luego podrás suscribirte para continuar usando DriverAI."
                }
            },
            "¡Teléfono verificado exitosamente! Ya puedes usar DriverAI."
        ));
    }
    
    // ==========================================
    // 3. REENVIAR CÓDIGO SMS
    // ==========================================
    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendCode([FromBody] ResendCodeRequest request)
    {
        // Validar formato de 8 dígitos
        if (!IsValidPhoneFormat(request.PhoneNumber))
        {
            return BadRequest(ApiResponse<object>.Error(
                "El teléfono debe tener exactamente 8 dígitos"));
        }
        
        // Agregar código de país +506 para buscar en BD
        var fullPhoneNumber = $"+506{request.PhoneNumber}";
        
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
        
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Error(
                "No se encontró un usuario con este número de teléfono."));
        }
        
        if (user.IsPhoneVerified)
        {
            return BadRequest(ApiResponse<object>.Error(
                "Este número de teléfono ya ha sido verificado."));
        }
        
        var newCode = GenerateRandomCode();
        user.SmsVerificationCode = newCode;
        user.SmsVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15);
        
        await _smsService.SendVerificationCodeAsync(fullPhoneNumber, newCode);
        await _context.SaveChangesAsync();
        
        return Ok(ApiResponse<object>.Ok(
            null,
            "Se ha enviado un nuevo código de verificación a tu teléfono."
        ));
    }
    
    // ==========================================
    // 4. LOGIN (con verificación de suscripción)
    // ==========================================
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Validar formato de 8 dígitos
        if (!IsValidPhoneFormat(request.PhoneNumber))
        {
            return Unauthorized(ApiResponse<object>.Error(
                "El teléfono debe tener exactamente 8 dígitos (ej: 88888888)"));
        }
        
        // Agregar código de país +506 para buscar en BD
        var fullPhoneNumber = $"+506{request.PhoneNumber}";
        
        // Buscar usuario por teléfono completo
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
        
        if (user == null)
        {
            return Unauthorized(ApiResponse<object>.Error(
                "Credenciales inválidas. Verifica tu número y contraseña."));
        }
        
        // Verificar si el teléfono está verificado
        if (!user.IsPhoneVerified)
        {
            return Unauthorized(ApiResponse<object>.Error(
                "Tu número de teléfono no ha sido verificado. Revisa tu SMS para el código de verificación.",
                new List<string> { "sms_unverified" }
            ));
        }
        
        // Verificar contraseña
        using var sha256 = SHA256.Create();
        var passwordHash = Convert.ToBase64String(
            sha256.ComputeHash(Encoding.UTF8.GetBytes(request.Password))
        );
        
        if (user.PasswordHash != passwordHash)
        {
            return Unauthorized(ApiResponse<object>.Error(
                "Credenciales inválidas. Verifica tu número y contraseña."));
        }
        
        // VERIFICAR ACCESO (suscripción o período gratuito)
        var hasAccess = user.HasAccess();
        
        if (!hasAccess)
        {
            return Unauthorized(ApiResponse<object>.Error(
                "Tu período de prueba ha expirado. Para seguir usando DriverAI, debes activar tu suscripción.",
                new List<string> { "subscription_expired" }
            ));
        }
        
        // Actualizar último login
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        // Generar token
        var token = _jwtService.GenerateToken(user.Id.ToString(), user.PhoneNumber);
        var displayPhone = user.PhoneNumber.Substring(4);
        
        // Crear información de suscripción
        var subscriptionInfo = new Models.SubscriptionInfo
        {
            HasAccess = true,
            IsInTrial = user.TrialEndDate > DateTime.UtcNow,
            RemainingTrialDays = user.GetRemainingTrialDays(),
            IsSubscriptionActive = user.IsSubscriptionActive && user.SubscriptionExpiryDate > DateTime.UtcNow,
            SubscriptionExpiryDate = user.SubscriptionExpiryDate,
            Message = user.TrialEndDate > DateTime.UtcNow 
                ? $"Te quedan {user.GetRemainingTrialDays()} días de prueba gratis."
                : "Suscripción activa. ¡Gracias por confiar en DriverAI!"
        };
        
        return Ok(ApiResponse<object>.Ok(
            new { 
                token,
                user = new { 
                    user.Id, 
                    phoneNumber = displayPhone,
                    user.Email, 
                    user.Username 
                },
                subscription = subscriptionInfo
            },
            "Login exitoso."
        ));
    }
    
    // ==========================================
    // 5. VERIFICAR ESTADO DE SUSCRIPCIÓN
    // ==========================================
    [HttpGet("subscription-status/{phoneNumber}")]
    public async Task<IActionResult> GetSubscriptionStatus(string phoneNumber)
    {
        // Validar formato de 8 dígitos
        if (!IsValidPhoneFormat(phoneNumber))
        {
            return BadRequest(ApiResponse<object>.Error(
                "El teléfono debe tener exactamente 8 dígitos"));
        }
        
        // Agregar código de país +506 para buscar en BD
        var fullPhoneNumber = $"+506{phoneNumber}";
        
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
        
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Error(
                "Usuario no encontrado."));
        }
        
        var hasAccess = user.HasAccess();
        
        var subscriptionInfo = new Models.SubscriptionInfo
        {
            HasAccess = hasAccess,
            IsInTrial = user.TrialEndDate > DateTime.UtcNow,
            RemainingTrialDays = user.GetRemainingTrialDays(),
            IsSubscriptionActive = user.IsSubscriptionActive && user.SubscriptionExpiryDate > DateTime.UtcNow,
            SubscriptionExpiryDate = user.SubscriptionExpiryDate,
            Message = !hasAccess
                ? "Tu período de prueba ha expirado. Activa tu suscripción para seguir usando DriverAI."
                : (user.TrialEndDate > DateTime.UtcNow
                    ? $"Te quedan {user.GetRemainingTrialDays()} días de prueba gratis."
                    : "Suscripción activa.")
        };
        
        return Ok(ApiResponse<object>.Ok(
            subscriptionInfo,
            hasAccess ? "Acceso permitido" : "Acceso denegado. Suscripción requerida."
        ));
    }
    
    // ==========================================
    // 6. ACTIVAR SUSCRIPCIÓN
    // ==========================================
    [HttpPost("activate-subscription")]
    public async Task<IActionResult> ActivateSubscription([FromBody] ActivateSubscriptionRequest request)
    {
        // Validar formato de 8 dígitos
        if (!IsValidPhoneFormat(request.PhoneNumber))
        {
            return BadRequest(ApiResponse<object>.Error(
                "El teléfono debe tener exactamente 8 dígitos"));
        }
        
        // Agregar código de país +506 para buscar en BD
        var fullPhoneNumber = $"+506{request.PhoneNumber}";
        
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
        
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Error("Usuario no encontrado."));
        }
        
        // Calcular nueva fecha de expiración
        DateTime newExpiryDate;
        if (user.SubscriptionExpiryDate > DateTime.UtcNow)
        {
            // Si ya tiene suscripción activa, extender
            newExpiryDate = user.SubscriptionExpiryDate.Value.AddMonths(request.Months);
        }
        else
        {
            // Si no tiene o expiró, empezar desde hoy
            newExpiryDate = DateTime.UtcNow.AddMonths(request.Months);
        }
        
        user.IsSubscriptionActive = true;
        user.SubscriptionExpiryDate = newExpiryDate;
        user.TrialEndDate = null; // Terminar período de prueba
        
        await _context.SaveChangesAsync();
        
        return Ok(ApiResponse<object>.Ok(
            new
            {
                subscriptionExpiryDate = newExpiryDate,
                message = $"¡Suscripción activada! Tu acceso está garantizado hasta {newExpiryDate:dd/MM/yyyy}"
            },
            "Suscripción activada exitosamente."
        ));
    }
    
    // ==========================================
    // MÉTODOS AUXILIARES
    // ==========================================
    
    private bool IsValidPhoneFormat(string phoneNumber)
    {
        // Validar que sean exactamente 8 dígitos
        return !string.IsNullOrEmpty(phoneNumber) && 
               phoneNumber.Length == 8 && 
               phoneNumber.All(char.IsDigit);
    }
    
    private string GenerateRandomCode()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }
}