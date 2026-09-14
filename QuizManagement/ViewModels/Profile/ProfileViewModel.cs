namespace QuizManagement.ViewModels.Profile
{
    public class ProfileViewModel
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? Role { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
