namespace DriverAI.API.Models.Entities;

public class VerificationCode
{
    public string Code { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
}
