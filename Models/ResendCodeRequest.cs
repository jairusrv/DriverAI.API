using System.ComponentModel.DataAnnotations;

namespace DriverAI.API.Models;

public class ResendCodeRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string CodeType { get; set; } = "email"; // "email" o "sms"
}