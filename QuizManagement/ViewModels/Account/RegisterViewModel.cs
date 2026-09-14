using System.ComponentModel.DataAnnotations;
using QuizManagement.Infrastructure;

namespace QuizManagement.ViewModels.Account
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Tên người dùng là bắt buộc.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên người dùng phải từ 3 đến 100 ký tự.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
        [StringLength(
            PasswordPolicy.MaximumLength,
            MinimumLength = PasswordPolicy.MinimumLength,
            ErrorMessage = "Mật khẩu phải có từ 8 đến 100 ký tự.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
