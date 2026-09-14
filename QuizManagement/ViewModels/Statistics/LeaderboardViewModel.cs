namespace QuizManagement.ViewModels.Statistics
{
    public class LeaderboardViewModel
    {
        public int DeckId { get; set; }
        public string DeckName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public List<LeaderboardEntryViewModel> Entries { get; set; } = new();
    }

    public class LeaderboardEntryViewModel
    {
        public int Rank { get; set; }
        public string Username { get; set; } = string.Empty;
        public double BestPercentage { get; set; }
        public int AttemptCount { get; set; }
        public DateTime LastAttemptAt { get; set; }
    }
}
