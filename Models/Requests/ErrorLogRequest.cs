namespace DriverAI.API.Models.Requests;

public class ErrorLogRequest
{
    public int? UserId { get; set; }

    public string Source { get; set; } = "";

    public string Message { get; set; } = "";

    public string StackTrace { get; set; } = "";

    public string DeviceInfo { get; set; } = "";
}