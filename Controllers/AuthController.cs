using System.Security.Cryptography;
using System.Text;
using DriverAI.API.Config;
using DriverAI.API.Models.Entities;
using DriverAI.API.Models.Requests;
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
    private readonly ILogger _logger;

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

            return BadRequest(new
            {
                success = false,
                message = "Formato de teléfono inválido.",
                errors
            });
        }

        var fullPhoneNumber = $"+506{request.PhoneNumber}";

        var existingUserByImei = await _context.Users
            .FirstOrDefaultAsync(u => u.Imei == request.Imei);

        if (existingUserByImei != null)
        {
            var hasAccess = existingUserByImei.HasAccess();

            return Conflict(new
            {
                success = false,
                message = "Este dispositivo ya está registrado.",
                errors = new List<string>
                {
                    "device_exists",
                    hasAccess ? "access_active" : "subscription_required"
                },
                data = new
                {
                    remainingTrialDays = existingUserByImei.GetRemainingTrialDays(),
                    subscriptionExpiryDate = existingUserByImei.SubscriptionExpiryDate
                }
            });
        }

        var existingPhone = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);

        if (existingPhone != null)
            errors.Add("El número de teléfono ya está registrado.");

        var existingEmail = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingEmail != null)
            errors.Add("El email ya está registrado con otra cuenta.");

        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (existingUser != null)
            errors.Add("El nombre de usuario ya está en uso.");

        User? referrer = null;
        var normalizedReferralCode = NormalizeReferralCode(request.ReferralCode);

        if (!string.IsNullOrWhiteSpace(normalizedReferralCode))
        {
            referrer = await _context.Users
                .FirstOrDefaultAsync(u => u.ReferralCode == normalizedReferralCode);

            if (referrer == null)
                errors.Add("El código de referido no existe.");
        }

        if (errors.Any())
        {
            return BadRequest(new
            {
                success = false,
                message = "No se pudo completar el registro.",
                errors
            });
        }

        using var sha256 = SHA256.Create();

        var passwordHash = Convert.ToBase64String(
            sha256.ComputeHash(
                Encoding.UTF8.GetBytes(request.Password)
            )
        );

        var trialEndDate = DateTime.UtcNow.AddDays(7);
        var referralCode = await GenerateUniqueReferralCode();

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
            SubscriptionExpiryDate = null,
            ReferralCode = referralCode,
            ReferredByCode = normalizedReferralCode,
            ReferredByUserId = referrer?.Id
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = _jwtService.GenerateToken(user);
        var displayPhone = user.PhoneNumber.Substring(4);

        return Ok(new
        {
            success = true,
            message = "Registro exitoso. Ya puedes usar DriverAI.",
            data = new
            {
                token,
                user = new
                {
                    user.Id,
                    phoneNumber = displayPhone,
                    user.Email,
                    user.Username,
                    user.ReferralCode
                },
                subscription = BuildSubscriptionResponse(user)
            }
        });
    }

    [HttpGet("subscription-status/{phoneNumber}")]
    public async Task<IActionResult> GetSubscriptionStatus(string phoneNumber)
    {
        if (!IsValidPhoneFormat(phoneNumber))
        {
            return BadRequest(new
            {
                success = false,
                message = "Teléfono debe tener 8 dígitos"
            });
        }

        var fullPhoneNumber = $"+506{phoneNumber}";

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);

        if (user == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Usuario no encontrado"
            });
        }

        return Ok(new
        {
            success = true,
            data = BuildSubscriptionResponse(user)
        });
    }

    [HttpGet("subscription-details/{phoneNumber}")]
    public async Task<IActionResult> GetSubscriptionDetails(string phoneNumber)
    {
        return await GetSubscriptionStatus(phoneNumber);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!IsValidPhoneFormat(request.PhoneNumber))
        {
            return Unauthorized(new
            {
                success = false,
                message = "El teléfono debe tener exactamente 8 dígitos"
            });
        }

        var fullPhoneNumber = $"+506{request.PhoneNumber}";

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == fullPhoneNumber);

        if (user == null)
        {
            return Unauthorized(new
            {
                success = false,
                message = "Credenciales inválidas."
            });
        }

        using var sha256 = SHA256.Create();

        var passwordHash = Convert.ToBase64String(
            sha256.ComputeHash(
                Encoding.UTF8.GetBytes(request.Password)
            )
        );

        if (user.PasswordHash != passwordHash)
        {
            return Unauthorized(new
            {
                success = false,
                message = "Credenciales inválidas."
            });
        }

        var hasAccess = user.HasAccess();

        if (!hasAccess)
        {
            return Unauthorized(new
            {
                success = false,
                message = "Tu período de prueba ha expirado. Activa tu suscripción.",
                errors = new List<string> { "subscription_expired" },
                data = BuildSubscriptionResponse(user)
            });
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var token = _jwtService.GenerateToken(user);
        var displayPhone = user.PhoneNumber.Substring(4);

        return Ok(new
        {
            success = true,
            message = "Login exitoso.",
            data = new
            {
                token,
                user = new
                {
                    user.Id,
                    phoneNumber = displayPhone,
                    user.Email,
                    user.Username,
                    user.ReferralCode
                },
                subscription = BuildSubscriptionResponse(user)
            }
        });
    }

    private object BuildSubscriptionResponse(User user)
    {
        var now = DateTime.UtcNow;
        var hasAccess = user.HasAccess();
        var isInTrial = user.TrialEndDate > now;
        var isSubscriptionActive =
            user.IsSubscriptionActive &&
            user.SubscriptionExpiryDate > now;

        return new
        {
            hasAccess,
            isInTrial,
            remainingTrialDays = user.GetRemainingTrialDays(),
            remainingDays = user.GetRemainingAccessDays(),
            isSubscriptionActive,
            trialEndDate = user.TrialEndDate,
            subscriptionExpiryDate = user.SubscriptionExpiryDate,
            referralCode = user.ReferralCode,
            referredByCode = user.ReferredByCode,
            referralPaidCount = user.ReferralPaidCount,
            referralRewardCount = user.ReferralRewardCount,
            referralsNeededForReward = 5 - (user.ReferralPaidCount % 5),
            lastReferralRewardMessage = user.LastReferralRewardMessage,
            message = hasAccess
                ? $"Acceso activo. Te quedan {user.GetRemainingAccessDays()} días."
                : "Suscripción requerida"
        };
    }

    private async Task<string> GenerateUniqueReferralCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        for (var attempt = 0; attempt < 25; attempt++)
        {
            var code = GenerateCode(chars, 6);

            var exists = await _context.Users
                .AnyAsync(u => u.ReferralCode == code);

            if (!exists)
                return code;
        }

        throw new Exception("No se pudo generar un código de referido único.");
    }

    private static string GenerateCode(string chars, int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];

        for (var i = 0; i < length; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }

        return new string(result);
    }

    private static string? NormalizeReferralCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToUpperInvariant();
    }

    private bool IsValidPhoneFormat(string phoneNumber)
    {
        return !string.IsNullOrEmpty(phoneNumber) &&
               phoneNumber.Length == 8 &&
               phoneNumber.All(char.IsDigit);
    }
}