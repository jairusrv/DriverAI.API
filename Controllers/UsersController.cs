using DriverAI.API.Config;
using DriverAI.API.Models.Entities;
using DriverAI.API.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace DriverAI.API.Controllers;

[ApiController]
[Route("users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Imei,
                x.PhoneNumber,
                x.Email,
                x.Username,
                x.IsEmailVerified,
                x.IsPhoneVerified,
                x.CreatedAt,
                x.TrialEndDate,
                x.IsSubscriptionActive,
                x.SubscriptionExpiryDate,
                x.LastLoginAt,
                HasAccess = x.HasAccess(),
                RemainingTrialDays = x.GetRemainingTrialDays()
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _db.Users
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Imei,
                x.PhoneNumber,
                x.Email,
                x.Username,
                x.IsEmailVerified,
                x.IsPhoneVerified,
                x.CreatedAt,
                x.TrialEndDate,
                x.IsSubscriptionActive,
                x.SubscriptionExpiryDate,
                x.LastLoginAt,
                HasAccess = x.HasAccess(),
                RemainingTrialDays = x.GetRemainingTrialDays()
            })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound(new
            {
                message = "Usuario no encontrado"
            });
        }

        return Ok(user);
    }

    [HttpGet("{id:int}/subscription")]
    public async Task<IActionResult> GetSubscription(int id)
    {
        var userExists = await _db.Users
            .AnyAsync(x => x.Id == id);

        if (!userExists)
        {
            return NotFound(new
            {
                message = "Usuario no encontrado"
            });
        }

        var subscriptions = await _db.UserSubscriptions
            .Where(x => x.UserId == id)
            .OrderByDescending(x => x.EndDate)
            .ToListAsync();

        return Ok(subscriptions);
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        UpdateUserStatusRequest request
    )
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Usuario no encontrado"
            });
        }

        user.IsSubscriptionActive = request.IsSubscriptionActive;
        user.SubscriptionExpiryDate = request.SubscriptionExpiryDate;

        if (request.IsSubscriptionActive)
        {
            var subscription = new UserSubscription
            {
                UserId = user.Id,
                Status = "ACTIVE",
                StartDate = DateTime.UtcNow,
                EndDate = request.SubscriptionExpiryDate ?? DateTime.UtcNow.AddDays(30),
                PaymentMethod = "MANUAL",
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _db.UserSubscriptions.Add(subscription);
        }
        else
        {
            var subscription = new UserSubscription
            {
                UserId = user.Id,
                Status = "INACTIVE",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow,
                PaymentMethod = "MANUAL",
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _db.UserSubscriptions.Add(subscription);
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Estado actualizado",
            user = new
            {
                user.Id,
                user.Email,
                user.Username,
                user.IsSubscriptionActive,
                user.SubscriptionExpiryDate,
                HasAccess = user.HasAccess()
            }
        });
    }

    [HttpPut("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Usuario no encontrado"
            });
        }

        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddDays(30);

        user.IsSubscriptionActive = true;
        user.SubscriptionExpiryDate = endDate;

        var subscription = new UserSubscription
        {
            UserId = user.Id,
            Status = "ACTIVE",
            StartDate = startDate,
            EndDate = endDate,
            PaymentMethod = "MANUAL",
            Notes = "Activado manualmente por administrador",
            CreatedAt = DateTime.UtcNow
        };

        _db.UserSubscriptions.Add(subscription);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Usuario activado",
            subscription
        });
    }

    [HttpPut("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Usuario no encontrado"
            });
        }

        user.IsSubscriptionActive = false;
        user.SubscriptionExpiryDate = DateTime.UtcNow;

        var subscription = new UserSubscription
        {
            UserId = user.Id,
            Status = "INACTIVE",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow,
            PaymentMethod = "MANUAL",
            Notes = "Desactivado manualmente por administrador",
            CreatedAt = DateTime.UtcNow
        };

        _db.UserSubscriptions.Add(subscription);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Usuario desactivado"
        });
    }

    [HttpPut("{id:int}/extend")]
    public async Task<IActionResult> Extend(
        int id,
        ExtendSubscriptionRequest request
    )
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Usuario no encontrado"
            });
        }

        var startDate = DateTime.UtcNow;

        if (user.SubscriptionExpiryDate != null &&
            user.SubscriptionExpiryDate > DateTime.UtcNow)
        {
            startDate = user.SubscriptionExpiryDate.Value;
        }

        var endDate = startDate.AddDays(request.Days);

        user.IsSubscriptionActive = true;
        user.SubscriptionExpiryDate = endDate;

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

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Suscripción extendida",
            subscription
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
        {
            return NotFound(new
            {
                message = "Usuario no encontrado"
            });
        }

        _db.Users.Remove(user);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Usuario eliminado"
        });
    }

    [HttpPost("{id:int}/promote-admin")]
public async Task<IActionResult> PromoteToAdmin(
    int id,
    PromoteAdminRequest request
)
{
    var user = await _db.Users
        .FirstOrDefaultAsync(x => x.Id == id);

    if (user == null)
    {
        return NotFound(new
        {
            message = "Usuario no encontrado"
        });
    }

    user.Role = "Admin";

    await _db.SaveChangesAsync();

    return Ok(new
    {
        message = "Usuario promovido a administrador",
        user = new
        {
            user.Id,
            user.Email,
            user.Username,
            user.Role
        },
        notes = request.Notes
    });
}
[HttpPost("{id:int}/demote-admin")]
public async Task<IActionResult> DemoteAdmin(
    int id
)
{
    var user = await _db.Users
        .FirstOrDefaultAsync(x => x.Id == id);

    if (user == null)
    {
        return NotFound(new
        {
            message = "Usuario no encontrado"
        });
    }

    user.Role = "User";

    await _db.SaveChangesAsync();

    return Ok(new
    {
        message = "Usuario degradado a usuario normal",
        user = new
        {
            user.Id,
            user.Email,
            user.Username,
            user.Role
        }
    });
}
}