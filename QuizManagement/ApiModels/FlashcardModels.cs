using Services;

namespace QuizManagement.ApiModels;

public sealed record ApiFlashcardOption(int Id, string Content, bool IsCorrect);

public sealed record ApiFlashcardProgress(int Repetition, DateTime? NextReviewAtUtc);

public sealed record ApiFlashcard(
    int QuestionId,
    string Content,
    int QuestionType,
    string? Explanation,
    IReadOnlyList<string> CorrectAnswers,
    IReadOnlyList<ApiFlashcardOption> Options,
    ApiFlashcardProgress? Progress);

public sealed record ApiFlashcardSession(
    int DeckId,
    string DeckName,
    string SubjectName,
    int TotalCards,
    int DueCards,
    bool StudyingAll,
    DateTime? NextReviewAtUtc,
    IReadOnlyList<ApiFlashcard> Cards)
{
    public static ApiFlashcardSession From(FlashcardSession session)
        => new(
            session.DeckId,
            session.DeckName,
            session.SubjectName,
            session.TotalCards,
            session.DueCards,
            session.StudyingAll,
            session.NextReviewAtUtc,
            session.Cards.Select(card => new ApiFlashcard(
                card.QuestionId,
                card.Content,
                card.QuestionType,
                card.Explanation,
                card.CorrectAnswers,
                card.Options.Select(opt => new ApiFlashcardOption(opt.Id, opt.Content, opt.IsCorrect)).ToList(),
                card.Repetition.HasValue
                    ? new ApiFlashcardProgress(card.Repetition.Value, card.NextReviewAtUtc)
                    : null
            )).ToList());
}

public sealed record ApiFlashcardReviewRequest(int DeckId, int QuestionId, bool Remembered, Guid? ReviewId = null);

public sealed record ApiFlashcardReviewResponse(
    int QuestionId,
    int Repetition,
    int IntervalMinutes,
    DateTime? NextReviewAtUtc)
{
    public static ApiFlashcardReviewResponse From(FlashcardReviewResult result)
        => new(result.QuestionId, result.Repetition, result.IntervalMinutes, result.NextReviewAtUtc);
}
