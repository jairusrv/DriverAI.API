namespace DriverAI.API.Models.Requests;

public class RideHistoryRequest
{
    public int UserId { get; set; }

    public double Fare { get; set; }

    public double DistanceKm { get; set; }

    public double PickupDistanceKm { get; set; }

    public double EstimatedTimeMinutes { get; set; }

    public double Profit { get; set; }

    public double ProfitPerKm { get; set; }

    public string Decision { get; set; } = "";

    public string SourceApp { get; set; } = "";
}