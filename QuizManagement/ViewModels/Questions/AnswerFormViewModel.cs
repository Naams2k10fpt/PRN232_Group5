using System.ComponentModel.DataAnnotations;

namespace QuizManagement.ViewModels.Questions
{
    public class AnswerFormViewModel
    {
        public int Id { get; set; }

        [StringLength(4000, ErrorMessage = "Nội dung đáp án quá dài.")]
        public string? Content { get; set; }

        public bool IsCorrect { get; set; }
    }
}
