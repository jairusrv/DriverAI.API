using System.ComponentModel.DataAnnotations;

namespace DriverAI.API.Models;

public class LoginRequest
{
    [Required(ErrorMessage = "El número de teléfono es requerido")]
    [Phone(ErrorMessage = "Formato de teléfono inválido")]
    [RegularExpression(@"^\+506[0-9]{8}$", ErrorMessage = "Formato inválido. Usa +506 seguido de 8 dígitos (ej: +50612345678)")]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "La contraseña es requerida")]
    public string Password { get; set; } = string.Empty;
}