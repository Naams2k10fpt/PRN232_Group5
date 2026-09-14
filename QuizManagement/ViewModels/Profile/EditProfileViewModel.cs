using System.ComponentModel.DataAnnotations;

namespace QuizManagement.ViewModels.Profile
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Tên người dùng là bắt buộc.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên người dùng phải từ 3 đến 100 ký tự.")]
        public string Username { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "URL avatar không được vượt quá 500 ký tự.")]
        [Url(ErrorMessage = "URL avatar không hợp lệ.")]
        public string? AvatarUrl { get; set; }
    }
}
