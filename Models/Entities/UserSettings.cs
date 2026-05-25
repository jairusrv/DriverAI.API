namespace DriverAI.API.Models.Entities;

public class UserSettings
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string FuelType { get; set; } = "Gasolina";

    public double FuelPrice { get; set; }

    public double KmPerLiter { get; set; }

    public double MinimumProfitPerKm { get; set; }

    public double MaxPickupDistance { get; set; }

    public double MaxTripDistance { get; set; }

    public string Currency { get; set; } = "CRC";

    public string Language { get; set; } = "es";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}