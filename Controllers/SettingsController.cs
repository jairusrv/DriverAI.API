using DriverAI.API.Config;
using DriverAI.API.Models.Entities;
using DriverAI.API.Models.Requests;
using DriverAI.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        if (!UserAccessHelper.CanAccessUser(User, userId))
        {
            return Forbid();
        }

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
    public async Task<IActionResult> CreateOrUpdate(
        UserSettingsRequest request
    )
    {
        if (!UserAccessHelper.CanAccessUser(User, request.UserId))
        {
            return Forbid();
        }

        if (request.MaintenanceCostPerKm is < 0)
        {
            return BadRequest(new
            {
                message = "El costo de mantenimiento por km no puede ser negativo"
            });
        }

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
        settings.ServiceType = request.ServiceType;
        settings.Platform = request.Platform;

        settings.VehicleType = request.VehicleType;
        settings.MaintenanceCostPerKm = request.MaintenanceCostPerKm;

        settings.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Configuración guardada",
            settings
        });
    }
}