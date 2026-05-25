using System.ComponentModel.DataAnnotations;

namespace DriverAI.API.Models.Requests;

public class LoginRequest
{
    [Required(ErrorMessage = "El número de teléfono es requerido")]
    [RegularExpression(@"^[0-9]{8}$", ErrorMessage = "El teléfono debe tener exactamente 8 dígitos (ej: 88888888)")]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "La contraseña es requerida")]
    public string Password { get; set; } = string.Empty;
}
