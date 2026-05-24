namespace DriverAI.API.Models;

public class User
{
    public int Id { get; set; }

    public string FullName { get; set; }
        = string.Empty;

    public string Email { get; set; }
        = string.Empty;

    public string Phone { get; set; }
        = string.Empty;

    public string Password { get; set; }
        = string.Empty;

    public bool IsActive { get; set; }
        = true;

    public DateTime TrialEndsAt
    {
        get;
        set;
    } = DateTime.UtcNow.AddDays(7);
}