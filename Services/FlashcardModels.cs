namespace Services
{
    public sealed record FlashcardOption(int Id, string Content, bool IsCorrect);

    // One flashcard: the question is the front; the correct answers and the explanation are the back.
    // Repetition and NextReviewAtUtc are null until the user reviews the card for the first time.
    public sealed record FlashcardCard(
        int QuestionId,
        string Content,
        int QuestionType,
        string? Explanation,
        IReadOnlyList<string> CorrectAnswers,
        IReadOnlyList<FlashcardOption> Options,
        int? Repetition,
        DateTime? NextReviewAtUtc);

    // The cards a user studies in one sitting. DueCards counts new cards and cards whose review time has
    // passed; NextReviewAtUtc is the earliest upcoming review among the cards that are not due yet.
    public sealed record FlashcardSession(
        int DeckId,
        string DeckName,
        string SubjectName,
        int TotalCards,
        int DueCards,
        bool StudyingAll,
        DateTime? NextReviewAtUtc,
        IReadOnlyList<FlashcardCard> Cards);

    public sealed record FlashcardReviewResult(
        int QuestionId,
        int Repetition,
        int IntervalMinutes,
        DateTime NextReviewAtUtc);
}
