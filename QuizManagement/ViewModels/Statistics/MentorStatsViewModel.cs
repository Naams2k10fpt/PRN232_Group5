namespace QuizManagement.ViewModels.Statistics
{
    public class MentorStatsViewModel
    {
        public int TotalAttempts { get; set; }
        public int UniqueUsers { get; set; }
        public double OverallAvgPercentage { get; set; }
        public double OverallBestPercentage { get; set; }
        public List<MentorSubjectStatViewModel> SubjectStats { get; set; } = new();
        public List<MentorDeckStatViewModel> DeckStats { get; set; } = new();
    }

    public class MentorSubjectStatViewModel
    {
        public string SubjectName { get; set; } = string.Empty;
        public int TotalAttempts { get; set; }
        public int UniqueUsers { get; set; }
        public double AvgPercentage { get; set; }
        public double BestPercentage { get; set; }
    }

    public class MentorDeckStatViewModel
    {
        public int DeckId { get; set; }
        public string DeckName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public int TotalAttempts { get; set; }
        public int UniqueUsers { get; set; }
        public double AvgPercentage { get; set; }
        public double BestPercentage { get; set; }
        public DateTime LastAttemptAt { get; set; }
    }
}
