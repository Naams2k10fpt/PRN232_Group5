using System.ComponentModel.DataAnnotations;

namespace QuizManagement.ViewModels.Decks
{
    public class DeckFormViewModel
    {
        public int Id { get; set; }

        [Required]
        public int SubjectId { get; set; }

        public string? SubjectName { get; set; }

        [Required(ErrorMessage = "Tên bộ đề là bắt buộc.")]
        [StringLength(255, ErrorMessage = "Tên bộ đề không được vượt quá 255 ký tự.")]
        public string Name { get; set; } = string.Empty;

        [Range(0, 180, ErrorMessage = "Giới hạn thời gian phải từ 0 (không giới hạn) đến 180 phút.")]
        [Display(Name = "Giới hạn thời gian làm bài")]
        public int TimeLimitMinutes { get; set; } = 0;
    }
}
