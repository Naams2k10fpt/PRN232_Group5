namespace Services
{
    public static class QuestionImportLimits
    {
        public const long MaxRequestBytes = 10 * 1024 * 1024;
        public const int MaxUploadBytes = 5 * 1024 * 1024;
        public const int MaxTextCharacters = 1_000_000;
        public const int MaxFormValues = 64_000;
        public const int MaxZipEntries = 128;
        public const long MaxEntryBytes = 8 * 1024 * 1024;
        public const long MaxTotalUncompressedBytes = 16 * 1024 * 1024;
        public const int MaxRows = 2_000;
        public const int MaxCellsPerRow = 64;
        public const int MaxAnswersPerQuestion = 8;
        public const int MaxSharedStrings = 50_000;
        public const int MaxCellCharacters = 32_767;
    }

    public class QuestionImportPreview
    {
        public List<QuestionImportRow> ValidRows { get; set; } = new();

        public List<QuestionImportError> Errors { get; set; } = new();
    }

    public class QuestionImportRow
    {
        public int RowNumber { get; set; }

        public string Content { get; set; } = string.Empty;

        public int QuestionType { get; set; }

        public string? Explanation { get; set; }

        public List<QuestionImportAnswer> Answers { get; set; } = new();
    }

    public class QuestionImportAnswer
    {
        public string Content { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }
    }

    public class QuestionImportError
    {
        public int RowNumber { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
