namespace DriverAI.API.Models.Entities;

public class RideHistory
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public double Fare { get; set; }

    public double DistanceKm { get; set; }

    public double PickupDistanceKm { get; set; }

    public double EstimatedTimeMinutes { get; set; }

    public double Profit { get; set; }

    public double ProfitPerKm { get; set; }

    public string Decision { get; set; } = "";

    public string SourceApp { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}