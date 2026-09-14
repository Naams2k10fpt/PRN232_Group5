using System.ComponentModel.DataAnnotations;

namespace QuizManagement.ViewModels.Quiz
{
    public class QuizConfigViewModel
    {
        public int DeckId { get; set; }

        public string DeckName { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số câu hỏi.")]
        [Range(1, 500, ErrorMessage = "Số câu hỏi phải từ 1 trở lên.")]
        [Display(Name = "Số câu hỏi")]
        public int QuestionCount { get; set; } = 10;

        public int AvailableQuestionCount { get; set; }

        // Populated from deck.TimeLimitMinutes — not user input
        public int TimeLimitMinutes { get; set; } = 0;
    }
}
