namespace DriverAI.API.Models.Entities;

public class Payment
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "CRC";

    public string Provider { get; set; } = "CARD";

    public string ProviderReference { get; set; } = "";

    public string Status { get; set; } = "PENDING";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }

    public string PaymentType { get; set; } = "SUBSCRIPTION";

    public DateTime? PaidFrom { get; set; }

    public DateTime? PaidUntil { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? ApprovedBy { get; set; }

    public string? Notes { get; set; }

    public string? SinpeSenderPhone { get; set; }

    public string? SinpeReferenceNumber { get; set; }
}