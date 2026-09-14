namespace QuizManagement.ViewModels.Quiz
{
    /// <summary>
    /// ViewModel nhận dữ liệu từ form submit bài quiz
    /// </summary>
    public class QuizSubmitViewModel
    {
        public int DeckId { get; set; }

        public string AttemptToken { get; set; } = string.Empty;

        public List<QuizQuestionSubmitItem> Questions { get; set; } = new();
    }

    public class QuizQuestionSubmitItem
    {
        public int QuestionId { get; set; }

        /// <summary>
        /// Đáp án được chọn cho single choice (radio button).
        /// </summary>
        public int? SelectedAnswerId { get; set; }

        /// <summary>
        /// Đáp án được chọn cho multiple choice (checkboxes).
        /// </summary>
        public List<int> SelectedAnswerIds { get; set; } = new();
    }
}
