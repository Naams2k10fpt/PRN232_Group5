namespace BusinessObjects;

public class LoginAttempt
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string IpAddress { get; set; } = null!;
    public bool IsSuccess { get; set; }
    public bool CountsTowardLockout { get; set; }
    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
