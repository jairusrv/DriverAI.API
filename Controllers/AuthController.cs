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
    // 1. REGISTRO - Primer paso
    // ==========================================
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var errors = new List<string>();
        
        // Validar email único
        var existingEmail = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);
        
        if (existingEmail != null)
        {
            errors.Add("El email ya está registrado. Por favor utiliza otro email.");
        }
        
        // Validar teléfono único
        var existingPhone = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber);
        
        if (existingPhone != null)
        {
            errors.Add("El número de teléfono ya está registrado.");
        }
        
        // Validar usuario único
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);
        
        if (existingUser != null)
        {
            errors.Add("El nombre de usuario ya está en uso. Por favor elige otro.");
        }
        
        if (errors.Any())
        {
            return BadRequest(ApiResponse<object>.Error(
                "No se pudo completar el registro. Por favor corrige los siguientes errores:",
                errors));
        }
        
        // Generar códigos de verificación
        var emailCode = GenerateRandomCode();
        var smsCode = GenerateRandomCode();
        
        // Crear hash de contraseña
        using var sha256 = SHA256.Create();
        var passwordHash = Convert.ToBase64String(
            sha256.ComputeHash(Encoding.UTF8.GetBytes(request.Password))
        );
        
        // Crear usuario (pendiente de verificación)
        var user = new User
        {
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Username = request.Username,
            PasswordHash = passwordHash,
            IsEmailConfirmed = false,
            IsPhoneConfirmed = false,
            EmailVerificationCode = emailCode,
            EmailVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15),
            SmsVerificationCode = smsCode,
            SmsVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        // Enviar códigos de verificación
        var emailSent = await _emailService.SendVerificationCodeAsync(request.Email, emailCode);
        var smsSent = await _smsService.SendVerificationCodeAsync(request.PhoneNumber, smsCode);
        
        var message = "Te hemos enviado códigos de verificación. ";
        if (emailSent) message += "Revisa tu correo electrónico. ";
        if (smsSent) message += "Revisa tu teléfono. ";
        
        return Ok(ApiResponse<object>.Ok(
            new { 
                email = request.Email,
                phoneNumber = request.PhoneNumber,
                requiresEmailVerification = true,
                requiresSmsVerification = true
            },
            message
        ));
    }
    
    // ==========================================
    // 2. VERIFICAR CÓDIGO (Email o SMS)
    // ==========================================
    [HttpPost("verify-code")]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);
        
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Error(
                "No se encontró un usuario con este email."));
        }
        
        if (request.CodeType == "email")
        {
            // Verificar código de email
            if (user.EmailVerificationCode != request.Code)
            {
                return BadRequest(ApiResponse<object>.Error(
                    "Código de verificación de email incorrecto."));
            }
            
            if (user.EmailVerificationCodeExpiry < DateTime.UtcNow)
            {
                return BadRequest(ApiResponse<object>.Error(
                    "El código de verificación de email ha expirado. Solicita uno nuevo."));
            }
            
            user.IsEmailConfirmed = true;
            user.EmailVerificationCode = null;
            user.EmailVerificationCodeExpiry = null;
        }
        else if (request.CodeType == "sms")
        {
            // Verificar código de SMS
            if (user.SmsVerificationCode != request.Code)
            {
                return BadRequest(ApiResponse<object>.Error(
                    "Código de verificación de teléfono incorrecto."));
            }
            
            if (user.SmsVerificationCodeExpiry < DateTime.UtcNow)
            {
                return BadRequest(ApiResponse<object>.Error(
                    "El código de verificación de teléfono ha expirado. Solicita uno nuevo."));
            }
            
            user.IsPhoneConfirmed = true;
            user.SmsVerificationCode = null;
            user.SmsVerificationCodeExpiry = null;
        }
        else
        {
            return BadRequest(ApiResponse<object>.Error(
                "Tipo de código inválido. Usa 'email' o 'sms'."));
        }
        
        await _context.SaveChangesAsync();
        
        // Verificar si el usuario ya está completamente verificado
        var isFullyVerified = user.IsEmailConfirmed && user.IsPhoneConfirmed;
        
        if (isFullyVerified)
        {
            // Generar token JWT
            var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email);
            
            return Ok(ApiResponse<object>.Ok(
                new { 
                    token,
                    user = new { 
                        user.Id, 
                        user.Email, 
                        user.PhoneNumber, 
                        user.Username 
                    }
                },
                "¡Cuenta verificada exitosamente! Ya puedes iniciar sesión."
            ));
        }
        
        return Ok(ApiResponse<object>.Ok(
            new { 
                verified = request.CodeType,
                remainingVerification = user.IsEmailConfirmed ? "sms" : "email"
            },
            $"¡{request.CodeType.ToUpper()} verificado correctamente! " +
            $"Ahora verifica tu {(user.IsEmailConfirmed ? "teléfono" : "email")}."
        ));
    }
    
    // ==========================================
    // 3. REENVIAR CÓDIGO
    // ==========================================
    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendCode([FromBody] ResendCodeRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);
        
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Error(
                "No se encontró un usuario con este email."));
        }
        
        if (request.CodeType == "email" && !user.IsEmailConfirmed)
        {
            var newCode = GenerateRandomCode();
            user.EmailVerificationCode = newCode;
            user.EmailVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15);
            
            await _emailService.SendVerificationCodeAsync(user.Email, newCode);
            await _context.SaveChangesAsync();
            
            return Ok(ApiResponse<object>.Ok(
                null,
                "Se ha enviado un nuevo código de verificación a tu email."
            ));
        }
        else if (request.CodeType == "sms" && !user.IsPhoneConfirmed)
        {
            var newCode = GenerateRandomCode();
            user.SmsVerificationCode = newCode;
            user.SmsVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15);
            
            await _smsService.SendVerificationCodeAsync(user.PhoneNumber, newCode);
            await _context.SaveChangesAsync();
            
            return Ok(ApiResponse<object>.Ok(
                null,
                "Se ha enviado un nuevo código de verificación a tu teléfono."
            ));
        }
        
        return BadRequest(ApiResponse<object>.Error(
            $"El {request.CodeType.ToUpper()} ya ha sido verificado o el usuario no existe."));
    }
    
    // ==========================================
    // 4. LOGIN
    // ==========================================
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Buscar usuario por email
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);
        
        if (user == null)
        {
            return Unauthorized(ApiResponse<object>.Error(
                "Credenciales inválidas. Verifica tu email y contraseña."));
        }
        
        // Verificar si la cuenta está completamente verificada
        if (!user.IsEmailConfirmed)
        {
            return Unauthorized(ApiResponse<object>.Error(
                "Tu cuenta no ha sido verificada. Revisa tu email para el código de verificación.",
                new List<string> { "email_unverified" }
            ));
        }
        
        if (!user.IsPhoneConfirmed)
        {
            return Unauthorized(ApiResponse<object>.Error(
                "Tu número de teléfono no ha sido verificado. Revisa tu teléfono para el código de verificación.",
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
                "Credenciales inválidas. Verifica tu email y contraseña."));
        }
        
        // Actualizar último login
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        // Generar token
        var token = _jwtService.GenerateToken(user.Id.ToString(), user.Email);
        
        return Ok(ApiResponse<object>.Ok(
            new { 
                token,
                user = new { 
                    user.Id, 
                    user.Email, 
                    user.PhoneNumber, 
                    user.Username 
                }
            },
            "Login exitoso. ¡Bienvenido a DriverAI!"
        ));
    }
    
    // ==========================================
    // MÉTODO AUXILIAR: Generar código de 6 dígitos
    // ==========================================
    private string GenerateRandomCode()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }
}