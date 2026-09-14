namespace QuizManagement.ViewModels.Quiz
{
    /// <summary>
    /// ViewModel cho màn hình làm bài quiz.
    /// KHÔNG chứa IsCorrect để tránh lộ đáp án cho client.
    /// </summary>
    public class QuizTakeViewModel
    {
        public int DeckId { get; set; }

        public string DeckName { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string AttemptToken { get; set; } = string.Empty;

        /// <summary>NULL = không giới hạn; giá trị do server tính từ expiry đã lưu.</summary>
        public int? TimeRemainingSeconds { get; set; }

        public List<QuizQuestionViewModel> Questions { get; set; } = new();
    }

    public class QuizQuestionViewModel
    {
        public int QuestionId { get; set; }

        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 1 = SingleChoice, 2 = MultipleChoice
        /// </summary>
        public int QuestionType { get; set; }

        public List<QuizAnswerOptionViewModel> Answers { get; set; } = new();
    }

    public class QuizAnswerOptionViewModel
    {
        public int AnswerId { get; set; }

        public string Content { get; set; } = string.Empty;
    }
}
