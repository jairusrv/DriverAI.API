namespace DriverAI.API.Models.Requests;

public class ExtendSubscriptionRequest
{
    public int Days { get; set; } = 30;

    public string PaymentMethod { get; set; } = "MANUAL";

    public string Notes { get; set; } = "";
}