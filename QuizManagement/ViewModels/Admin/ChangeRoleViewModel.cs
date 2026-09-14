using System.ComponentModel.DataAnnotations;
using BusinessObjects;

namespace QuizManagement.ViewModels.Admin
{
    public class ChangeRoleViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        public string CurrentUsername { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
        public string NewRole { get; set; } = string.Empty;
    }
}
