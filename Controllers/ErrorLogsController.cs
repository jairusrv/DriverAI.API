using DriverAI.API.Config;
using DriverAI.API.Models.Entities;
using DriverAI.API.Models.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DriverAI.API.Controllers;

[ApiController]
[Route("errors")]
public class ErrorLogsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ErrorLogsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var logs = await _db.ErrorLogs
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync();

        return Ok(logs);
    }

    [HttpGet("user/{userId:int}")]
    public async Task<IActionResult> GetByUserId(int userId)
    {
        var logs = await _db.ErrorLogs
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync();

        return Ok(logs);
    }

    [HttpGet("source/{source}")]
    public async Task<IActionResult> GetBySource(string source)
    {
        var logs = await _db.ErrorLogs
            .Where(x => x.Source.ToLower() == source.ToLower())
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync();

        return Ok(logs);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ErrorLogRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new
            {
                message = "El mensaje del error es obligatorio"
            });
        }

        var error = new ErrorLog
        {
            UserId = request.UserId,
            Source = request.Source,
            Message = request.Message,
            StackTrace = request.StackTrace,
            DeviceInfo = request.DeviceInfo,
            CreatedAt = DateTime.UtcNow
        };

        _db.ErrorLogs.Add(error);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Error registrado",
            error
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var error = await _db.ErrorLogs
            .FirstOrDefaultAsync(x => x.Id == id);

        if (error == null)
        {
            return NotFound(new
            {
                message = "Error no encontrado"
            });
        }

        _db.ErrorLogs.Remove(error);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Error eliminado"
        });
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> Clear()
    {
        var logs = await _db.ErrorLogs.ToListAsync();

        _db.ErrorLogs.RemoveRange(logs);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Logs eliminados",
            count = logs.Count
        });
    }
}