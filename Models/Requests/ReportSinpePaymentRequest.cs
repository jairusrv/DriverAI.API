namespace DriverAI.API.Models.Requests;

public class ReportSinpePaymentRequest
{
    public decimal Amount { get; set; }

    public string SinpeSenderPhone { get; set; } = "";

    public string SinpeReferenceNumber { get; set; } = "";

    public string? Notes { get; set; }
}