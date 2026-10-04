namespace BusinessObjects;

// Read models for the study catalog (subjects, decks). Projected straight from SQL, never tracked.

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class SubjectSummaryReadModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string OwnerName { get; init; } = string.Empty;
    public int DeckCount { get; init; }
    public int QuestionCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class DeckSummaryReadModel
{
    public int Id { get; init; }
    public int SubjectId { get; init; }
    public string SubjectName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int TimeLimitMinutes { get; init; }
    public int QuestionCount { get; init; }

    // Flashcards the user should study now: never reviewed, or whose next review time has passed.
    public int DueCount { get; init; }

    // Flashcards whose latest review by the user was "remembered".
    public int LearnedCount { get; init; }

    // Earliest upcoming review among the cards that are not due yet; null when there is none.
    public DateTime? NextReviewAtUtc { get; init; }
}
