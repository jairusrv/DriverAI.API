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
    // 1. REGISTRO (envía códigos a email y SMS)
    // ==========================================
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var errors = new List<string>();
        
        // Validar formato de 8 dígitos
        if (!IsValidPhoneFormat(request.PhoneNumber))
        {
            errors.Add("El número de teléfono debe tener exactamente 8 dígitos (ej: 88888888)");
            return BadRequest(ApiResponse<object>.Error("Formato de teléfono inválido.", errors));
        }
        
        var fullPhoneNumber = $"+506{request.PhoneNumber}";
        
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
        
        // Generar códigos
        var emailCode = GenerateRandomCode();
        var smsCode = GenerateRandomCode();
        
        // Hash de contraseña
        using var sha256 = SHA256.Create();
        var passwordHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(request.Password)));
        
        var trialEndDate = DateTime.UtcNow.AddDays(7);
        
        var user = new User
        {
            PhoneNumber = fullPhoneNumber,
            Email = request.Email,
            Username = request.Username,
            PasswordHash = passwordHash,
            IsPhoneVerified = false,
            SmsVerificationCode = smsCode,
            SmsVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15),
            IsEmailVerified = false,
            EmailVerificationCode = emailCode,
            EmailVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow,
            TrialEndDate = trialEndDate,
            IsSubscriptionActive = false,
            SubscriptionExpiryDate = null
        };
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        // Enviar código por email y SMS
        var emailSent = await _emailService.SendVerificationCodeAsync(request.Email, emailCode);
        var smsSent = await _smsService.SendVerificationCodeAsync(fullPhoneNumber, smsCode);
        
        return Ok(ApiResponse<object>.Ok(
            new { 
                phoneNumber = request.PhoneNumber,
                email = request.Email,
                trialEndDate = trialEndDate,
                freeTrialDays = 7,
                requiresEmailVerification = true,
                requiresSmsVerification = true
            },
            emailSent && smsSent 
                ? "Te hemos enviado códigos de verificación a tu email y teléfono."
                : "No se pudieron enviar todos los códigos. Intenta nuevamente."
        ));
    }
    
    // ==========================================
    // 2. VERIFICAR CÓDIGO DE EMAIL
    // ==========================================
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
            return NotFound(ApiResponse<object>.Error("Usuario no encontrado."));
        
        if (user.IsEmailVerified)
            return BadRequest(ApiResponse<object>.Error("El email ya ha sido verificado."));
        
        if (user.EmailVerificationCode != request.Code)
            return BadRequest(ApiResponse<object>.Error("Código de email incorrecto."));
        
        if (user.EmailVerificationCodeExpiry < DateTime.UtcNow)
            return BadRequest(ApiResponse<object>.Error("El código ha expirado. Solicita uno nuevo."));
        
        user.IsEmailVerified = true;
        user.EmailVerificationCode = null;
        user.EmailVerificationCodeExpiry = null;
        await _context.SaveChangesAsync();
        
        // Si el teléfono ya estaba verificado, generar token
        if (user.IsPhoneVerified)
        {
            var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email);
            return Ok(ApiResponse<object>.Ok(new { token }, "Email verificado exitosamente."));
        }
        
        return Ok(ApiResponse<object>.Ok(null, "Email verificado. Ahora verifica tu teléfono."));
    }
    
    // ==========================================
    // 3. REENVIAR CÓDIGO DE EMAIL
    // ==========================================
    [HttpPost("resend-email-code")]
    public async Task<IActionResult> ResendEmailCode([FromBody] ResendEmailCodeRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
            return NotFound(ApiResponse<object>.Error("Usuario no encontrado."));
        
        if (user.IsEmailVerified)
            return BadRequest(ApiResponse<object>.Error("El email ya está verificado."));
        
        var newCode = GenerateRandomCode();
        user.EmailVerificationCode = newCode;
        user.EmailVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15);
        await _emailService.SendVerificationCodeAsync(user.Email, newCode);
        await _context.SaveChangesAsync();
        
        return Ok(ApiResponse<object>.Ok(null, "Nuevo código enviado a tu email."));
    }
    
    // ==========================================
    // 4. VERIFICAR CÓDIGO SMS (ya existente)
    // ==========================================
    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request)
    {
        if (!IsValidPhoneFormat(request.PhoneNumber))
            return BadRequest(ApiResponse<object>.Error("El teléfono debe tener exactamente 8 dígitos"));
        
        var fullPhoneNumber = $"+506{request.PhoneNumber}";
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
        
        if (user == null)
            return NotFound(ApiResponse<object>.Error("Usuario no encontrado."));
        
        if (user.IsPhoneVerified)
            return BadRequest(ApiResponse<object>.Error("El teléfono ya está verificado."));
        
        if (user.SmsVerificationCode != request.Code)
            return BadRequest(ApiResponse<object>.Error("Código SMS incorrecto."));
        
        if (user.SmsVerificationCodeExpiry < DateTime.UtcNow)
            return BadRequest(ApiResponse<object>.Error("El código ha expirado. Solicita uno nuevo."));
        
        user.IsPhoneVerified = true;
        user.SmsVerificationCode = null;
        user.SmsVerificationCodeExpiry = null;
        await _context.SaveChangesAsync();
        
        if (user.IsEmailVerified)
        {
            var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email);
            return Ok(ApiResponse<object>.Ok(new { token }, "Teléfono verificado. Acceso concedido."));
        }
        
        return Ok(ApiResponse<object>.Ok(null, "Teléfono verificado. Ahora verifica tu email."));
    }
    
    // ==========================================
    // 5. REENVIAR CÓDIGO SMS
    // ==========================================
    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendCode([FromBody] ResendCodeRequest request)
    {
        if (!IsValidPhoneFormat(request.PhoneNumber))
            return BadRequest(ApiResponse<object>.Error("El teléfono debe tener exactamente 8 dígitos"));
        
        var fullPhoneNumber = $"+506{request.PhoneNumber}";
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
        
        if (user == null)
            return NotFound(ApiResponse<object>.Error("Usuario no encontrado."));
        
        if (user.IsPhoneVerified)
            return BadRequest(ApiResponse<object>.Error("El teléfono ya está verificado."));
        
        var newCode = GenerateRandomCode();
        user.SmsVerificationCode = newCode;
        user.SmsVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15);
        await _smsService.SendVerificationCodeAsync(fullPhoneNumber, newCode);
        await _context.SaveChangesAsync();
        
        return Ok(ApiResponse<object>.Ok(null, "Nuevo código enviado a tu teléfono."));
    }
    
    // ==========================================
    // 6. LOGIN (verifica que ambos campos estén verificados)
    // ==========================================
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!IsValidPhoneFormat(request.PhoneNumber))
            return Unauthorized(ApiResponse<object>.Error("El teléfono debe tener exactamente 8 dígitos"));
        
        var fullPhoneNumber = $"+506{request.PhoneNumber}";
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);
        
        if (user == null)
            return Unauthorized(ApiResponse<object>.Error("Credenciales inválidas."));
        
        if (!user.IsEmailVerified)
            return Unauthorized(ApiResponse<object>.Error("Debes verificar tu email primero.", new List<string> { "email_unverified" }));
        
        if (!user.IsPhoneVerified)
            return Unauthorized(ApiResponse<object>.Error("Debes verificar tu teléfono primero.", new List<string> { "sms_unverified" }));
        
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
    
    // ==========================================
    // MÉTODOS AUXILIARES
    // ==========================================
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