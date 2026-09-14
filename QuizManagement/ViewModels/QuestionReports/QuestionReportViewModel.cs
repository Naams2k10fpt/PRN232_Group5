using System.ComponentModel.DataAnnotations;

namespace QuizManagement.ViewModels.QuestionReports
{
    public class SubmitReportViewModel
    {
        public int QuestionId { get; set; }

        public string QuestionContent { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn lý do báo cáo.")]
        [StringLength(100, ErrorMessage = "Lý do báo cáo tối đa 100 ký tự.")]
        [RegularExpression("^(WrongAnswer|UnclearQuestion|DuplicateQuestion|Other)$",
            ErrorMessage = "Lý do báo cáo không hợp lệ.")]
        [Display(Name = "Lý do")]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
        [Display(Name = "Ghi chú thêm (tuỳ chọn)")]
        public string? Note { get; set; }

        // Where to go back after submit
        public int? TestHistoryId { get; set; }
    }

    public class QuestionReportListItemViewModel
    {
        public int Id { get; set; }
        public int QuestionId { get; set; }
        public string QuestionContent { get; set; } = string.Empty;
        public string DeckName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string ReporterUsername { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string? Note { get; set; }
        public bool IsResolved { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
