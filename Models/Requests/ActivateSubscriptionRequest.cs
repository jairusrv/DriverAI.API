using System.ComponentModel.DataAnnotations;

namespace DriverAI.API.Models.Requests;

public class ActivateSubscriptionRequest
{
    [Required(ErrorMessage = "El número de teléfono es requerido")]
    [RegularExpression(@"^[0-9]{8}$", ErrorMessage = "El teléfono debe tener exactamente 8 dígitos")]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required]
    [Range(1, 12)]
    public int Months { get; set; } = 1;
}
