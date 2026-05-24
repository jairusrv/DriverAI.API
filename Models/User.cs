using System.ComponentModel.DataAnnotations;

namespace DriverAI.API.Models;

public class User
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Relación con datos de Recope (si aplica)
    public ICollection<RecopeData>? RecopeDataList { get; set; }
}