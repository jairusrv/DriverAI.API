using System.ComponentModel.DataAnnotations;

namespace DriverAI.API.Models.Entities;

public class VerificationCode
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Type { get; set; } = string.Empty;
    // Ejemplos: SMS, EMAIL

    public bool IsUsed { get; set; } = false;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}