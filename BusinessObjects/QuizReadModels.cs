namespace BusinessObjects;

public sealed class TestHistoryPage
{
    public List<TestHistory> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class AnalyticsGroupReadModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ParentName { get; init; }
    public int Attempts { get; init; }
    public int UniqueUsers { get; init; }
    public double AveragePercentage { get; init; }
    public double BestPercentage { get; init; }
    public DateTime LastAttemptAt { get; init; }
}

public sealed class ScorePointReadModel
{
    public DateTime CreatedAt { get; init; }
    public double Percentage { get; init; }
}

public sealed class UserStatisticsReadModel
{
    public int TotalAttempts { get; init; }
    public double AveragePercentage { get; init; }
    public double BestPercentage { get; init; }
    public double LowestPercentage { get; init; }
    public int PassedAttempts { get; init; }
    public List<ScorePointReadModel> RecentScores { get; init; } = [];
    public List<AnalyticsGroupReadModel> SubjectStats { get; init; } = [];
    public List<AnalyticsGroupReadModel> DeckStats { get; init; } = [];
}

public sealed class MentorStatisticsReadModel
{
    public int TotalAttempts { get; init; }
    public int UniqueUsers { get; init; }
    public double AveragePercentage { get; init; }
    public double BestPercentage { get; init; }
    public List<AnalyticsGroupReadModel> SubjectStats { get; init; } = [];
    public List<AnalyticsGroupReadModel> DeckStats { get; init; } = [];
}

public sealed class LeaderboardEntryReadModel
{
    public string Username { get; init; } = string.Empty;
    public double BestPercentage { get; init; }
    public int AttemptCount { get; init; }
    public DateTime LastAttemptAt { get; init; }
}
