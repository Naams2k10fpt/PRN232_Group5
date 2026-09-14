using System.ComponentModel.DataAnnotations;
using QuizManagement.Infrastructure;

namespace QuizManagement.ViewModels.Profile
{
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc.")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
        [StringLength(
            PasswordPolicy.MaximumLength,
            MinimumLength = PasswordPolicy.MinimumLength,
            ErrorMessage = "Mật khẩu mới phải có từ 8 đến 100 ký tự.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
