using System.ComponentModel.DataAnnotations;

namespace DriverAI.API.Models;

public class ResendCodeRequest
{
    [Required(ErrorMessage = "El número de teléfono es requerido")]
    [RegularExpression(@"^[0-9]{8}$", ErrorMessage = "El teléfono debe tener exactamente 8 dígitos")]
    public string PhoneNumber { get; set; } = string.Empty;
}