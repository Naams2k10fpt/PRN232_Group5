namespace Services
{
    public interface IFlashcardService
    {
        // Returns null when the deck does not exist. With includeAll = false only due cards are returned.
        FlashcardSession? GetStudySession(string userId, int deckId, bool includeAll);

        // Returns null when the question does not belong to the deck (or was deleted); nothing is saved then.
        FlashcardReviewResult? Review(string userId, int deckId, int questionId, bool remembered);
    }
}
