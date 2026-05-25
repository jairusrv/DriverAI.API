namespace DriverAI.API.Models.Requests;

public class UpdateUserStatusRequest
{
    public bool IsSubscriptionActive { get; set; }

    public DateTime? SubscriptionExpiryDate { get; set; }

    public string Notes { get; set; } = "";
}