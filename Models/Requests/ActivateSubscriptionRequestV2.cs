namespace DriverAI.API.Models.Requests;

public class ActivateSubscriptionRequestV2
{
    public int UserId { get; set; }

    public int Days { get; set; } = 30;

    public string PaymentMethod { get; set; } = "CARD";

    public string Notes { get; set; } = "";
}