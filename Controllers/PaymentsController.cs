using System.Security.Claims;
using DriverAI.API.Config;
using DriverAI.API.Models.Entities;
using DriverAI.API.Models.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DriverAI.API.Controllers;

[ApiController]
[Route("payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PaymentsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        var payments = await _db.Payments
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(payments);
    }

    [HttpGet("user/{userId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetByUserId(int userId)
    {
        var userExists = await _db.Users
            .AnyAsync(x => x.Id == userId);

        if (!userExists)
        {
            return NotFound(new
            {
                message = "Usuario no existe"
            });
        }

        var payments = await _db.Payments
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(payments);
    }

    [HttpGet("user/{userId:int}/summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetSummary(int userId)
    {
        var userExists = await _db.Users
            .AnyAsync(x => x.Id == userId);

        if (!userExists)
        {
            return NotFound(new
            {
                message = "Usuario no existe"
            });
        }

        var approvedPayments = await _db.Payments
            .Where(x =>
                x.UserId == userId &&
                x.Status.ToUpper() == "APPROVED")
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        return Ok(new
        {
            totalPayments = approvedPayments.Count,
            totalAmount = approvedPayments.Sum(x => x.Amount),
            firstPayment = approvedPayments.FirstOrDefault(),
            lastPayment = approvedPayments.LastOrDefault()
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(PaymentRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == request.UserId);

        if (user == null)
        {
            return BadRequest(new
            {
                message = "Usuario no existe"
            });
        }

        if (request.Amount < 0)
        {
            return BadRequest(new
            {
                message = "El monto no puede ser negativo"
            });
        }

        var payment = new Payment
        {
            UserId = request.UserId,
            Amount = request.Amount,
            Currency = request.Currency,
            Provider = request.Provider,
            ProviderReference = request.ProviderReference,
            Status = NormalizeStatus(request.Status),
            PaymentType = NormalizePaymentType(request.PaymentType),
            Notes = request.Notes,
            SinpeSenderPhone = request.SinpeSenderPhone,
            SinpeReferenceNumber = request.SinpeReferenceNumber,
            CreatedAt = DateTime.UtcNow
        };

        _db.Payments.Add(payment);

        if (IsApproved(payment.Status))
        {
            ApplySubscriptionPeriod(user, payment, 30);
            CreateSubscriptionRecord(user, payment);
            await ApplyReferralRewardIfNeeded(user);
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Pago registrado",
            payment,
            subscriptionExpiryDate = user.SubscriptionExpiryDate
        });
    }

    [HttpPost("report-sinpe")]
    [Authorize]
    public async Task<IActionResult> ReportSinpePayment(
        ReportSinpePaymentRequest request
    )
    {
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                message = "No se pudo identificar el usuario."
            });
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == userId.Value);

        if (user == null)
        {
            return BadRequest(new
            {
                message = "Usuario no existe"
            });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new
            {
                message = "El monto debe ser mayor a cero."
            });
        }

        if (string.IsNullOrWhiteSpace(request.SinpeSenderPhone))
        {
            return BadRequest(new
            {
                message = "El teléfono SINPE es requerido."
            });
        }

        if (string.IsNullOrWhiteSpace(request.SinpeReferenceNumber))
        {
            return BadRequest(new
            {
                message = "La referencia SINPE es requerida."
            });
        }

        var existingPending = await _db.Payments.AnyAsync(x =>
            x.UserId == user.Id &&
            x.Status == "PENDING" &&
            x.Provider == "SINPE_MOVIL" &&
            x.SinpeReferenceNumber == request.SinpeReferenceNumber);

        if (existingPending)
        {
            return Conflict(new
            {
                message = "Ya existe un pago pendiente con esa referencia."
            });
        }

        var payment = new Payment
        {
            UserId = user.Id,
            Amount = request.Amount,
            Currency = "CRC",
            Provider = "SINPE_MOVIL",
            ProviderReference = request.SinpeReferenceNumber,
            Status = "PENDING",
            PaymentType = "SUBSCRIPTION",
            SinpeSenderPhone = request.SinpeSenderPhone.Trim(),
            SinpeReferenceNumber = request.SinpeReferenceNumber.Trim(),
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Pago SINPE reportado. Quedará pendiente de aprobación.",
            payment
        });
    }

    [HttpPost("activate-subscription")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ActivateSubscription(
        ActivateSubscriptionRequestV2 request
    )
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == request.UserId);

        if (user == null)
        {
            return BadRequest(new
            {
                message = "Usuario no existe"
            });
        }

        var startDate = GetSubscriptionBaseDate(user);
        var endDate = startDate.AddDays(request.Days);

        var subscription = new UserSubscription
        {
            UserId = user.Id,
            Status = "ACTIVE",
            StartDate = startDate,
            EndDate = endDate,
            PaymentMethod = request.PaymentMethod,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _db.UserSubscriptions.Add(subscription);

        user.IsSubscriptionActive = true;
        user.SubscriptionExpiryDate = endDate;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Suscripción activada",
            subscription
        });
    }

    [HttpPost("{paymentId:int}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApprovePayment(int paymentId)
    {
        var payment = await _db.Payments
            .FirstOrDefaultAsync(x => x.Id == paymentId);

        if (payment == null)
        {
            return NotFound(new
            {
                message = "Pago no encontrado"
            });
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == payment.UserId);

        if (user == null)
        {
            return BadRequest(new
            {
                message = "Usuario no existe"
            });
        }

        if (IsApproved(payment.Status))
        {
            return Ok(new
            {
                message = "El pago ya estaba aprobado",
                payment,
                subscriptionExpiryDate = user.SubscriptionExpiryDate
            });
        }

        payment.Status = "APPROVED";

        ApplySubscriptionPeriod(user, payment, 30);
        CreateSubscriptionRecord(user, payment);

        await ApplyReferralRewardIfNeeded(user);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Pago aprobado y suscripción activada",
            payment,
            subscriptionExpiryDate = user.SubscriptionExpiryDate
        });
    }

    [HttpPost("{paymentId:int}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RejectPayment(int paymentId)
    {
        var payment = await _db.Payments
            .FirstOrDefaultAsync(x => x.Id == paymentId);

        if (payment == null)
        {
            return NotFound(new
            {
                message = "Pago no encontrado"
            });
        }

        if (IsApproved(payment.Status))
        {
            return BadRequest(new
            {
                message = "No se puede rechazar un pago ya aprobado."
            });
        }

        payment.Status = "REJECTED";
        payment.ApprovedAt = DateTime.UtcNow;
        payment.ApprovedBy = GetAdminName();

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Pago rechazado",
            payment
        });
    }

    private void ApplySubscriptionPeriod(
        User user,
        Payment payment,
        int days
    )
    {
        var paidFrom = GetSubscriptionBaseDate(user);
        var paidUntil = paidFrom.AddDays(days);

        payment.PaidFrom = paidFrom;
        payment.PaidUntil = paidUntil;
        payment.ApprovedAt = DateTime.UtcNow;
        payment.ApprovedBy = GetAdminName();

        user.IsSubscriptionActive = true;
        user.SubscriptionExpiryDate = paidUntil;
    }

    private void CreateSubscriptionRecord(
        User user,
        Payment payment
    )
    {
        var subscription = new UserSubscription
        {
            UserId = user.Id,
            Status = "ACTIVE",
            StartDate = payment.PaidFrom ?? DateTime.UtcNow,
            EndDate = payment.PaidUntil ?? DateTime.UtcNow.AddDays(30),
            PaymentMethod = payment.Provider,
            Notes =
                $"Pago aprobado. Ref: {payment.ProviderReference}",
            CreatedAt = DateTime.UtcNow
        };

        _db.UserSubscriptions.Add(subscription);
    }

    private async Task ApplyReferralRewardIfNeeded(User payingUser)
    {
        if (payingUser.ReferredByUserId == null)
            return;

        var referrer = await _db.Users
            .FirstOrDefaultAsync(x =>
                x.Id == payingUser.ReferredByUserId.Value);

        if (referrer == null)
            return;

        referrer.ReferralPaidCount += 1;

        if (referrer.ReferralPaidCount % 5 != 0)
            return;

        var rewardPayment = new Payment
        {
            UserId = referrer.Id,
            Amount = 0,
            Currency = "CRC",
            Provider = "SYSTEM",
            ProviderReference = "",
            Status = "APPROVED",
            PaymentType = "REFERRAL_REWARD",
            Notes =
                $"Premio por {referrer.ReferralPaidCount} referidos pagados.",
            CreatedAt = DateTime.UtcNow
        };

        ApplySubscriptionPeriod(referrer, rewardPayment, 30);

        rewardPayment.ApprovedBy = "SYSTEM";

        _db.Payments.Add(rewardPayment);

        var rewardSubscription = new UserSubscription
        {
            UserId = referrer.Id,
            Status = "ACTIVE",
            StartDate = rewardPayment.PaidFrom ?? DateTime.UtcNow,
            EndDate = rewardPayment.PaidUntil ?? DateTime.UtcNow.AddDays(30),
            PaymentMethod = "REFERRAL_REWARD",
            Notes =
                $"Premio por {referrer.ReferralPaidCount} referidos pagados.",
            CreatedAt = DateTime.UtcNow
        };

        _db.UserSubscriptions.Add(rewardSubscription);

        referrer.ReferralRewardCount += 1;
        referrer.LastReferralRewardMessage =
            $"Ganaste 30 días gratis por alcanzar {referrer.ReferralPaidCount} referidos pagados.";
    }

    private static DateTime GetSubscriptionBaseDate(User user)
    {
        var now = DateTime.UtcNow;

        if (user.SubscriptionExpiryDate != null &&
            user.SubscriptionExpiryDate > now)
        {
            return user.SubscriptionExpiryDate.Value;
        }

        if (user.TrialEndDate != null &&
            user.TrialEndDate > now)
        {
            return user.TrialEndDate.Value;
        }

        return now;
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim =
            User.FindFirst("id")?.Value ??
            User.FindFirst("userId")?.Value ??
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }

    private string GetAdminName()
    {
        return User.Identity?.Name ??
               User.FindFirst(ClaimTypes.Name)?.Value ??
               User.FindFirst("username")?.Value ??
               "ADMIN";
    }

    private static bool IsApproved(string status)
    {
        return status.Equals("APPROVED", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("PAID", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return "PENDING";

        return status.Trim().ToUpperInvariant();
    }

    private static string NormalizePaymentType(string paymentType)
    {
        if (string.IsNullOrWhiteSpace(paymentType))
            return "SUBSCRIPTION";

        return paymentType.Trim().ToUpperInvariant();
    }
}