using BusinessObjects;

namespace QuizManagement.ApiModels;

public sealed record ApiPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record ApiSubject(
    int Id,
    string Name,
    string OwnerName,
    int DeckCount,
    int QuestionCount,
    DateTime CreatedAt)
{
    public static ApiSubject From(SubjectSummaryReadModel subject)
        => new(
            subject.Id,
            subject.Name,
            subject.OwnerName,
            subject.DeckCount,
            subject.QuestionCount,
            subject.CreatedAt);
}

// DueCount, LearnedCount and NextReviewAtUtc describe the current user's flashcard progress in the deck.
public sealed record ApiDeck(
    int Id,
    int SubjectId,
    string SubjectName,
    string Name,
    int TimeLimitMinutes,
    int QuestionCount,
    int DueCount,
    int LearnedCount,
    DateTime? NextReviewAtUtc)
{
    public static ApiDeck From(DeckSummaryReadModel deck)
        => new(
            deck.Id,
            deck.SubjectId,
            deck.SubjectName,
            deck.Name,
            deck.TimeLimitMinutes,
            deck.QuestionCount,
            deck.DueCount,
            deck.LearnedCount,
            deck.NextReviewAtUtc);
}
