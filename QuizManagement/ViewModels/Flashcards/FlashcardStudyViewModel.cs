namespace QuizManagement.ViewModels.Flashcards
{
    public class FlashcardStudyViewModel
    {
        public int DeckId { get; set; }

        public string DeckName { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public int TotalCards { get; set; }

        public int DueCards { get; set; }

        public bool StudyingAll { get; set; }

        public DateTime? NextReviewAtUtc { get; set; }

        public List<FlashcardViewModel> Cards { get; set; } = new();
    }

    public class FlashcardViewModel
    {
        public int QuestionId { get; set; }

        public string Content { get; set; } = string.Empty;

        public int QuestionType { get; set; }

        public string? Explanation { get; set; }

        public List<string> CorrectAnswers { get; set; } = new();

        public List<FlashcardAnswerOption> AllAnswers { get; set; } = new();
    }

    public class FlashcardAnswerOption
    {
        public string Content { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }
    }
}
