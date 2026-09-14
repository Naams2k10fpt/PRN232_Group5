namespace QuizManagement.ViewModels.Admin
{
    public class UserDetailViewModel
    {
        public AdminUserViewModel User { get; set; } = null!;
        public int SubjectCount { get; set; }
        public int DeckCount { get; set; }
        public int QuestionCount { get; set; }
        public int TestHistoryCount { get; set; }
    }
}
