namespace QuizManagement.ViewModels.Statistics
{
    public class StatisticsViewModel
    {
        public int TotalAttempts { get; set; }

        public double AveragePercentage { get; set; }

        public double BestPercentage { get; set; }

        public double LowestPercentage { get; set; }

        public int PassedAttempts { get; set; }

        public int FailedAttempts { get; set; }

        public double PassRate { get; set; }

        public List<ScoreTrendPointViewModel> RecentScores { get; set; } = new();

        public List<GroupPerformanceViewModel> SubjectStats { get; set; } = new();

        public List<GroupPerformanceViewModel> DeckStats { get; set; } = new();
    }

    public class ScoreTrendPointViewModel
    {
        public string Label { get; set; } = string.Empty;

        public double Percentage { get; set; }
    }

    public class GroupPerformanceViewModel
    {
        public string Name { get; set; } = string.Empty;

        public int Attempts { get; set; }

        public double AveragePercentage { get; set; }

        public double BestPercentage { get; set; }

        public DateTime LastAttemptAt { get; set; }
    }
}