using System.ComponentModel.DataAnnotations;

namespace DriverAI.API.Models.Requests;

public class RegisterRequest
{
    [Required(ErrorMessage = "El IMEI del dispositivo es requerido")]
    public string Imei { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "El número de teléfono es requerido")]
    [RegularExpression(@"^[0-9]{8}$", ErrorMessage = "El teléfono debe tener exactamente 8 dígitos (ej: 88888888)")]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "El email es requerido")]
    [EmailAddress(ErrorMessage = "Formato de email inválido")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "El nombre de usuario es requerido")]
    [MinLength(3, ErrorMessage = "El usuario debe tener al menos 3 caracteres")]
    [MaxLength(50, ErrorMessage = "El usuario no puede tener más de 50 caracteres")]
    public string Username { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "La contraseña es requerida")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
    public string Password { get; set; } = string.Empty;
}
