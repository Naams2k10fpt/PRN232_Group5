using System.ComponentModel.DataAnnotations;

namespace QuizManagement.ViewModels.Questions
{
    public class QuestionFormViewModel
    {
        public int Id { get; set; }

        public string? RowVersion { get; set; }

        [Required]
        public int DeckId { get; set; }

        public string? DeckName { get; set; }

        public int? SubjectId { get; set; }

        public string? SubjectName { get; set; }

        [Required(ErrorMessage = "Nội dung câu hỏi là bắt buộc.")]
        public string Content { get; set; } = string.Empty;

        public string? Explanation { get; set; }

        [Range(1, 2, ErrorMessage = "Loại câu hỏi không hợp lệ.")]
        public int QuestionType { get; set; } = 1;

        public List<AnswerFormViewModel> Answers { get; set; } =
        [
            new AnswerFormViewModel(),
            new AnswerFormViewModel()
        ];
    }
}
