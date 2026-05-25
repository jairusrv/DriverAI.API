using System.ComponentModel.DataAnnotations;

namespace DriverAI.API.Models.Entities;

public class User
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Imei { get; set; } = string.Empty;
    
    [Required]
    [Phone]
    [MaxLength(15)]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MinLength(3)]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = "User";
    // Auto-verificados
    public bool IsEmailVerified { get; set; } = true;
    public bool IsPhoneVerified { get; set; } = true;
    public string? EmailVerificationCode { get; set; }
    public DateTime? EmailVerificationCodeExpiry { get; set; }
    public string? SmsVerificationCode { get; set; }
    public DateTime? SmsVerificationCodeExpiry { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? TrialEndDate { get; set; }
    public bool IsSubscriptionActive { get; set; } = false;
    public DateTime? SubscriptionExpiryDate { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
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
    
}
