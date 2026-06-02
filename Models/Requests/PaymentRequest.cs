namespace DriverAI.API.Models.Requests;

public class PaymentRequest
{
    public int UserId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "CRC";

    public string Provider { get; set; } = "SINPE_MOVIL";

    public string ProviderReference { get; set; } = "";

    public string Status { get; set; } = "PENDING";

    public string PaymentType { get; set; } = "SUBSCRIPTION";

    public string? Notes { get; set; }

    public string? SinpeSenderPhone { get; set; }

    public string? SinpeReferenceNumber { get; set; }
}