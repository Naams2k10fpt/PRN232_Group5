using BusinessObjects;

namespace QuizManagement.ViewModels.Home
{
    public class DashboardViewModel
    {
        public int TotalQuizzesTaken { get; set; }

        public double AveragePercentage { get; set; }

        public DateTime? LastQuizDate { get; set; }

        public List<TestHistory> RecentHistories { get; set; } = new();

        public List<Subject> Subjects { get; set; } = new();
    }
}
