namespace DriverAI.API.Models.Entities;

public class UserSubscription
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Status { get; set; } = "TRIAL";

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime EndDate { get; set; } = DateTime.UtcNow.AddDays(7);

    public string PaymentMethod { get; set; } = "TRIAL";

    public string Notes { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}