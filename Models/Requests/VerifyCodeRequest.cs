using System.ComponentModel.DataAnnotations;

namespace DriverAI.API.Models.Requests;

public class VerifyCodeRequest
{
    [Required(ErrorMessage = "El número de teléfono es requerido")]
    [RegularExpression(@"^[0-9]{8}$", ErrorMessage = "El teléfono debe tener exactamente 8 dígitos")]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "El código es requerido")]
    [MinLength(6, ErrorMessage = "El código debe tener 6 dígitos")]
    [MaxLength(6, ErrorMessage = "El código debe tener 6 dígitos")]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "El código debe contener solo 6 dígitos")]
    public string Code { get; set; } = string.Empty;
}
