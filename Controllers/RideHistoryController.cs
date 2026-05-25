using DriverAI.API.Config;
using DriverAI.API.Models.Entities;
using DriverAI.API.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using DriverAI.API.Security;

namespace DriverAI.API.Controllers;

[ApiController]
[Route("history")]
[Authorize(Roles = "Admin")]
public class RideHistoryController : ControllerBase
{
    private readonly AppDbContext _db;

    public RideHistoryController(AppDbContext db)
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
        var userExists = await _db.Users
            .AnyAsync(x => x.Id == userId);

        if (!userExists)
        {
            return NotFound(new
            {
                message = "Usuario no existe"
            });
        }

        var history = await _db.RideHistory
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(history);
    }

    [HttpGet("{userId:int}/summary")]
    public async Task<IActionResult> GetSummary(int userId)
    {
        if (!UserAccessHelper.CanAccessUser(User, userId))
{
    return Forbid();
}
        var userExists = await _db.Users
            .AnyAsync(x => x.Id == userId);

        if (!userExists)
        {
            return NotFound(new
            {
                message = "Usuario no existe"
            });
        }

        var rides = await _db.RideHistory
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var total = rides.Count;
        var accepted = rides.Count(x => x.Decision.ToUpper() == "ACEPTAR");
        var rejected = rides.Count(x => x.Decision.ToUpper() == "RECHAZAR");

        var totalProfit = rides.Sum(x => x.Profit);
        var averageProfitPerKm = rides.Any()
            ? rides.Average(x => x.ProfitPerKm)
            : 0;

        return Ok(new
        {
            total,
            accepted,
            rejected,
            totalProfit,
            averageProfitPerKm
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(RideHistoryRequest request)
    {
        if (!UserAccessHelper.CanAccessUser(User, request.UserId))
{
    return Forbid();
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

        var ride = new RideHistory
        {
            UserId = request.UserId,
            Fare = request.Fare,
            DistanceKm = request.DistanceKm,
            PickupDistanceKm = request.PickupDistanceKm,
            EstimatedTimeMinutes = request.EstimatedTimeMinutes,
            Profit = request.Profit,
            ProfitPerKm = request.ProfitPerKm,
            Decision = request.Decision,
            SourceApp = request.SourceApp,
            CreatedAt = DateTime.UtcNow
        };

        _db.RideHistory.Add(ride);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Historial guardado",
            ride
        });
    }

    [HttpDelete("{id:int}")]
public async Task<IActionResult> Delete(int id)
{
    var ride = await _db.RideHistory
        .FirstOrDefaultAsync(x => x.Id == id);

    if (ride == null)
    {
        return NotFound(new
        {
            message = "Registro no encontrado"
        });
    }

    if (!UserAccessHelper.CanAccessUser(User, ride.UserId))
    {
        return Forbid();
    }

    _db.RideHistory.Remove(ride);

    await _db.SaveChangesAsync();

    return Ok(new
    {
        message = "Registro eliminado"
    });
}
}