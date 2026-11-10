using BusinessObjects;
using QuizManagement.Helpers;

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

        public static StatisticsViewModel From(UserStatisticsReadModel statistics)
        {
            var total = statistics.TotalAttempts;
            return new StatisticsViewModel
            {
                TotalAttempts = total,
                AveragePercentage = Math.Round(statistics.AveragePercentage, 1),
                BestPercentage = Math.Round(statistics.BestPercentage, 1),
                LowestPercentage = Math.Round(statistics.LowestPercentage, 1),
                PassedAttempts = statistics.PassedAttempts,
                FailedAttempts = total - statistics.PassedAttempts,
                PassRate = total > 0 ? Math.Round((double)statistics.PassedAttempts / total * 100, 1) : 0,
                RecentScores = statistics.RecentScores.Select(score => new ScoreTrendPointViewModel
                {
                    Label = VietnamTime.FromUtc(score.CreatedAt).ToString("dd/MM"),
                    Percentage = score.Percentage
                }).ToList(),
                SubjectStats = statistics.SubjectStats.Select(group => BuildGroup(group.Name, group)).ToList(),
                DeckStats = statistics.DeckStats.Select(group => BuildGroup($"{group.ParentName} / {group.Name}", group)).ToList()
            };
        }

        private static GroupPerformanceViewModel BuildGroup(string name, AnalyticsGroupReadModel group)
            => new()
            {
                Name = name,
                Attempts = group.Attempts,
                AveragePercentage = Math.Round(group.AveragePercentage, 1),
                BestPercentage = Math.Round(group.BestPercentage, 1),
                LastAttemptAt = group.LastAttemptAt
            };
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
