namespace DriverAI.API.Models;

public class VerificationCode
{
    public string Code { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
}