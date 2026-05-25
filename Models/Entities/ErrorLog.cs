namespace DriverAI.API.Models.Entities;

public class ErrorLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string Source { get; set; } = "";

    public string Message { get; set; } = "";

    public string StackTrace { get; set; } = "";

    public string DeviceInfo { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}