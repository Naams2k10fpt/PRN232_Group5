using System.ComponentModel.DataAnnotations;
using Services;

namespace QuizManagement.ApiModels;

public sealed class ReviewFlashcardRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "deckId không hợp lệ.")]
    public int DeckId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "questionId không hợp lệ.")]
    public int QuestionId { get; set; }

    // Nullable so a missing value is reported instead of silently meaning "not remembered".
    [Required(ErrorMessage = "remembered là bắt buộc.")]
    public bool? Remembered { get; set; }
}

public sealed record ApiFlashcardOption(int Id, string Content, bool IsCorrect);

public sealed record ApiFlashcardProgress(int Repetition, DateTime NextReviewAtUtc);

// Front of the card: Content (Markdown). Back: CorrectAnswers and Explanation (Markdown).
// Options lists every answer so the app can also show the card as a multiple-choice question.
// Progress is null until the user reviews the card for the first time.
public sealed record ApiFlashcard(
    int QuestionId,
    string Content,
    int QuestionType,
    string? Explanation,
    IReadOnlyList<string> CorrectAnswers,
    IReadOnlyList<ApiFlashcardOption> Options,
    ApiFlashcardProgress? Progress)
{
    public static ApiFlashcard From(FlashcardCard card)
        => new(
            card.QuestionId,
            card.Content,
            card.QuestionType,
            card.Explanation,
            card.CorrectAnswers,
            card.Options.Select(option => new ApiFlashcardOption(option.Id, option.Content, option.IsCorrect)).ToList(),
            card.Repetition is int repetition && card.NextReviewAtUtc is DateTime nextReviewAtUtc
                ? new ApiFlashcardProgress(repetition, nextReviewAtUtc)
                : null);
}

public sealed record ApiFlashcardDeck(
    int DeckId,
    string DeckName,
    string SubjectName,
    int TotalCards,
    int DueCards,
    bool StudyingAll,
    DateTime? NextReviewAtUtc,
    IReadOnlyList<ApiFlashcard> Cards)
{
    public static ApiFlashcardDeck From(FlashcardSession session)
        => new(
            session.DeckId,
            session.DeckName,
            session.SubjectName,
            session.TotalCards,
            session.DueCards,
            session.StudyingAll,
            session.NextReviewAtUtc,
            session.Cards.Select(ApiFlashcard.From).ToList());
}

public sealed record ReviewFlashcardResponse(
    int QuestionId,
    int Repetition,
    int IntervalMinutes,
    DateTime NextReviewAtUtc)
{
    public static ReviewFlashcardResponse From(FlashcardReviewResult result)
        => new(result.QuestionId, result.Repetition, result.IntervalMinutes, result.NextReviewAtUtc);
}
