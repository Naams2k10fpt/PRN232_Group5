using Microsoft.AspNetCore.Http;

namespace QuizManagement.ViewModels.Import
{
    public class QuestionImportInputViewModel
    {
        public int DeckId { get; set; }

        public string DeckName { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public IFormFile? ExcelFile { get; set; }

        public string? RawText { get; set; }

        public string TextTemplate { get; set; } = string.Empty;
    }

    public class QuestionImportPreviewViewModel
    {
        public int DeckId { get; set; }

        public string DeckName { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public string SourceName { get; set; } = string.Empty;

        public List<QuestionImportRowViewModel> ValidRows { get; set; } = new();

        public List<QuestionImportErrorViewModel> Errors { get; set; } = new();
    }

    public class QuestionImportRowViewModel
    {
        public int RowNumber { get; set; }

        public string Content { get; set; } = string.Empty;

        public int QuestionType { get; set; }

        public string? Explanation { get; set; }

        public List<QuestionImportAnswerViewModel> Answers { get; set; } = new();
    }

    public class QuestionImportAnswerViewModel
    {
        public string Content { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }
    }

    public class QuestionImportErrorViewModel
    {
        public int RowNumber { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}