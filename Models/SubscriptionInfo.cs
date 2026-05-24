namespace DriverAI.API.Models;

public class SubscriptionInfo
{
    public bool HasAccess { get; set; }
    public bool IsInTrial { get; set; }
    public int RemainingTrialDays { get; set; }
    public bool IsSubscriptionActive { get; set; }
    public DateTime? SubscriptionExpiryDate { get; set; }
    public string? Message { get; set; }
}