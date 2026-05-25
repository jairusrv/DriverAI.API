using DriverAI.API.Config;
using DriverAI.API.Models.Entities;
using DriverAI.API.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace DriverAI.API.Controllers;

[ApiController]
[Route("settings")]
[Authorize(Roles = "Admin")]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SettingsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{userId:int}")]
    public async Task<IActionResult> GetByUserId(int userId)
    {
        var settings = await _db.UserSettings
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (settings == null)
        {
            return NotFound(new
            {
                message = "Configuración no encontrada"
            });
        }

        return Ok(settings);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrUpdate(UserSettingsRequest request)
    {
        var userExists = await _db.Users
            .AnyAsync(x => x.Id == request.UserId);

        if (!userExists)
        {
            return BadRequest(new
            {
                message = "Usuario no existe"
            });
        }

        var settings = await _db.UserSettings
            .FirstOrDefaultAsync(x => x.UserId == request.UserId);

        if (settings == null)
        {
            settings = new UserSettings
            {
                UserId = request.UserId,
                CreatedAt = DateTime.UtcNow
            };

            _db.UserSettings.Add(settings);
        }

        settings.FuelType = request.FuelType;
        settings.FuelPrice = request.FuelPrice;
        settings.KmPerLiter = request.KmPerLiter;
        settings.MinimumProfitPerKm = request.MinimumProfitPerKm;
        settings.MaxPickupDistance = request.MaxPickupDistance;
        settings.MaxTripDistance = request.MaxTripDistance;
        settings.Currency = request.Currency;
        settings.Language = request.Language;
        settings.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Configuración guardada",
            settings
        });
    }
}