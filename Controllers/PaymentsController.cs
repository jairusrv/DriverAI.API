using DriverAI.API.Config;
using DriverAI.API.Models.Entities;
using DriverAI.API.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

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
            CreatedAt = DateTime.UtcNow
        };

        _db.Payments.Add(payment);

        if (request.Status.ToUpper() == "APPROVED" ||
            request.Status.ToUpper() == "PAID")
        {
            var subscription = new UserSubscription
            {
                UserId = user.Id,
                Status = "ACTIVE",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(30),
                PaymentMethod = request.Provider,
                Notes = $"Pago aprobado. Ref: {request.ProviderReference}",
                CreatedAt = DateTime.UtcNow
            };

            _db.UserSubscriptions.Add(subscription);

            user.IsSubscriptionActive = true;
            user.SubscriptionExpiryDate = subscription.EndDate;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Pago registrado",
            payment
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

        var startDate = DateTime.UtcNow;
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

        payment.Status = "APPROVED";

        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddDays(30);

        var subscription = new UserSubscription
        {
            UserId = user.Id,
            Status = "ACTIVE",
            StartDate = startDate,
            EndDate = endDate,
            PaymentMethod = payment.Provider,
            Notes = $"Pago aprobado manualmente. Ref: {payment.ProviderReference}",
            CreatedAt = DateTime.UtcNow
        };

        _db.UserSubscriptions.Add(subscription);

        user.IsSubscriptionActive = true;
        user.SubscriptionExpiryDate = endDate;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Pago aprobado y suscripción activada",
            payment,
            subscription
        });
    }
}