using DriverAI.API.Config;
using DriverAI.API.Models.Entities;
using DriverAI.API.Models.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DriverAI.API.Controllers;

[ApiController]
[Route("payments")]
[Authorize(Roles = "Admin")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PaymentsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var payments = await _db.Payments
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(payments);
    }

    [HttpGet("user/{userId:int}")]
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

    [HttpPost]
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

        var payment = new Payment
        {
            UserId = request.UserId,
            Amount = request.Amount,
            Currency = request.Currency,
            Provider = request.Provider,
            ProviderReference = request.ProviderReference,
            Status = request.Status,
            PaymentType = request.PaymentType,
            Notes = request.Notes,
            SinpeSenderPhone = request.SinpeSenderPhone,
            SinpeReferenceNumber = request.SinpeReferenceNumber,
            CreatedAt = DateTime.UtcNow
        };

        _db.Payments.Add(payment);

        if (IsApproved(request.Status))
        {
            await ExtendSubscriptionFromPayment(
                user,
                request.Provider,
                request.ProviderReference
            );

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

    [HttpPost("activate-subscription")]
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
    [HttpGet("user/{userId:int}/summary")]
    public async Task<IActionResult> GetSummary(
    int userId
)
    {
        var payments = await _db.Payments
            .Where(x =>
                x.UserId == userId &&
                x.Status == "APPROVED")
            .ToListAsync();

        return Ok(new
        {
            totalPayments = payments.Count,

            totalAmount =
                payments.Sum(x => x.Amount),

            firstPayment =
                payments.MinBy(x => x.CreatedAt),

            lastPayment =
                payments.MaxBy(x => x.CreatedAt)
        });
    }

    [HttpPost("{paymentId:int}/approve")]
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

        var paidFrom = GetSubscriptionBaseDate(user);

        var paidUntil = paidFrom.AddDays(30);

        payment.PaidFrom = paidFrom;

        payment.PaidUntil = paidUntil;

        payment.ApprovedAt = DateTime.UtcNow;

        payment.ApprovedBy =
            User.Identity?.Name ??
            "ADMIN";

        await ExtendSubscriptionFromPayment(
            user,
            payment.Provider,
            payment.ProviderReference
        );

        await ApplyReferralRewardIfNeeded(user);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Pago aprobado y suscripción activada",
            payment,
            subscriptionExpiryDate = user.SubscriptionExpiryDate
        });


    }

    private async Task ExtendSubscriptionFromPayment(
        User user,
        string provider,
        string providerReference
    )
    {
        var startDate = GetSubscriptionBaseDate(user);
        var endDate = startDate.AddDays(30);

        var subscription = new UserSubscription
        {
            UserId = user.Id,
            Status = "ACTIVE",
            StartDate = startDate,
            EndDate = endDate,
            PaymentMethod = provider,
            Notes = $"Pago aprobado. Ref: {providerReference}",
            CreatedAt = DateTime.UtcNow
        };

        _db.UserSubscriptions.Add(subscription);

        user.IsSubscriptionActive = true;
        user.SubscriptionExpiryDate = endDate;

        await Task.CompletedTask;
    }

    private async Task ApplyReferralRewardIfNeeded(User payingUser)
    {
        if (payingUser.ReferredByUserId == null)
            return;

        var referrer = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == payingUser.ReferredByUserId.Value);

        if (referrer == null)
            return;

        referrer.ReferralPaidCount += 1;

        if (referrer.ReferralPaidCount % 5 != 0)
            return;

        var startDate = GetSubscriptionBaseDate(referrer);
        var endDate = startDate.AddDays(30);

        var rewardSubscription = new UserSubscription
        {
            UserId = referrer.Id,
            Status = "ACTIVE",
            StartDate = startDate,
            EndDate = endDate,
            PaymentMethod = "REFERRAL_REWARD",
            Notes = $"Premio por {referrer.ReferralPaidCount} referidos pagados.",
            CreatedAt = DateTime.UtcNow
        };

        _db.UserSubscriptions.Add(rewardSubscription);

        referrer.IsSubscriptionActive = true;
        referrer.SubscriptionExpiryDate = endDate;
        referrer.ReferralRewardCount += 1;
        referrer.LastReferralRewardMessage =
            $"Ganaste 30 días gratis por alcanzar {referrer.ReferralPaidCount} referidos pagados.";

        _db.Payments.Add(
            new Payment
            {
                UserId = referrer.Id,

                Amount = 0,

                Currency = "CRC",

                Provider = "SYSTEM",

                ProviderReference = "",

                Status = "APPROVED",

                PaymentType = "REFERRAL_REWARD",

                PaidFrom = startDate,

                PaidUntil = endDate,

                ApprovedAt = DateTime.UtcNow,

                ApprovedBy = "SYSTEM",

                Notes =
            $"Premio por {referrer.ReferralPaidCount} referidos pagados.",

                CreatedAt = DateTime.UtcNow
            });
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

    private static bool IsApproved(string status)
    {
        return status.Equals("APPROVED", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("PAID", StringComparison.OrdinalIgnoreCase);
    }
}