using System.ComponentModel.DataAnnotations;

namespace DriverAI.API.Models;

public class User
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [Phone]
    [RegularExpression(@"^\+506[0-9]{8}$", ErrorMessage = "Formato inválido. Debe ser +506 seguido de 8 dígitos (ej: +50612345678)")]
    [MaxLength(12)] // +506 + 8 dígitos = 12 caracteres
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress(ErrorMessage = "Formato de email inválido")]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MinLength(3, ErrorMessage = "El usuario debe tener al menos 3 caracteres")]
    [MaxLength(50, ErrorMessage = "El usuario no puede tener más de 50 caracteres")]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    // Verificación por SMS
    public bool IsPhoneVerified { get; set; } = false;
    public string? SmsVerificationCode { get; set; }
    public DateTime? SmsVerificationCodeExpiry { get; set; }
    
    // Período gratuito
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TrialEndDate { get; set; }
    public bool IsSubscriptionActive { get; set; } = true;
    public DateTime? SubscriptionExpiryDate { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsEmailVerified { get; set; } = false;
    public string? EmailVerificationCode { get; set; }
    public DateTime? EmailVerificationCodeExpiry { get; set; }

    [Required]
    [MaxLength(100)]
    public string Imei { get; set; } = string.Empty;
    
    public bool HasAccess()
    {
        if (IsSubscriptionActive && SubscriptionExpiryDate > DateTime.UtcNow)
            return true;
        
        if (TrialEndDate > DateTime.UtcNow)
            return true;
        
        return false;
    }
    
    public int GetRemainingTrialDays()
    {
        if (TrialEndDate == null) return 0;
        var remaining = (TrialEndDate.Value - DateTime.UtcNow).Days;
        return remaining > 0 ? remaining : 0;
    }
    
    // Método para formatear número a +506XXXXXXXX
    public static string FormatPhoneNumber(string phoneNumber)
    {
        // Remover cualquier caracter no numérico
        var cleaned = new string(phoneNumber.Where(char.IsDigit).ToArray());
        
        // Si tiene 8 dígitos, agregar +506
        if (cleaned.Length == 8)
        {
            return $"+506{cleaned}";
        }
        
        // Si tiene 10 dígitos y comienza con 506, agregar +
        if (cleaned.Length == 10 && cleaned.StartsWith("506"))
        {
            return $"+{cleaned}";
        }
        
        // Si ya tiene formato válido, devolverlo
        if (cleaned.Length == 12 && cleaned.StartsWith("506"))
        {
            return $"+{cleaned}";
        }
        
        return phoneNumber;
    }
    
    // Método para extraer solo los 8 dígitos
    public static string GetLocalNumber(string formattedPhone)
    {
        if (formattedPhone.StartsWith("+506"))
        {
            return formattedPhone.Substring(4); // Quitar +506
        }
        return formattedPhone;
    }
}