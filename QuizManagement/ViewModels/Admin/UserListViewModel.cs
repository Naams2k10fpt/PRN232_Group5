namespace QuizManagement.ViewModels.Admin
{
    public class UserListViewModel
    {
        public List<AdminUserViewModel> Users { get; set; } = [];
        public string? SearchQuery { get; set; }
        public string? RoleFilter { get; set; }
    }
}
