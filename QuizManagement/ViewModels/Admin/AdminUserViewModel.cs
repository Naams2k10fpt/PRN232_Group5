namespace QuizManagement.ViewModels.Admin;

public class AdminUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsDisabled { get; set; }
    public DateTime CreatedAt { get; set; }
}
