namespace DriverAI.API.Models.Requests;

public class UserSettingsRequest
{
    public int UserId { get; set; }

    public string FuelType { get; set; } = "Gasolina";

    public double FuelPrice { get; set; }

    public double KmPerLiter { get; set; }

    public double MinimumProfitPerKm { get; set; }

    public double MaxPickupDistance { get; set; }

    public double MaxTripDistance { get; set; }

    public string Currency { get; set; } = "CRC";

    public string Language { get; set; } = "es";

    public string ServiceType { get; set; } = "Driver";

    public string Platform { get; set; } = "Uber";

    public string? VehicleType { get; set; }

    public double? MaintenanceCostPerKm { get; set; }
}