using System.ComponentModel.DataAnnotations;

namespace DriverAI.API.Models;

public class VerifyCodeRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MinLength(6)]
    [MaxLength(6)]
    public string Code { get; set; } = string.Empty;
    
    [Required]
    public string CodeType { get; set; } = "email"; // "email" o "sms"
}