using System.ComponentModel.DataAnnotations;

namespace QuizManagement.ViewModels.Subjects
{
    public class SubjectFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên môn học là bắt buộc.")]
        [StringLength(255, ErrorMessage = "Tên môn học không được vượt quá 255 ký tự.")]
        public string Name { get; set; } = string.Empty;
    }
}
