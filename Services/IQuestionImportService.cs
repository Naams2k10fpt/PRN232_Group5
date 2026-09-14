namespace Services
{
    public interface IQuestionImportService
    {
        QuestionImportPreview ParseText(string text);

        QuestionImportPreview ParseExcel(Stream stream);

        QuestionImportPreview ValidateRows(IEnumerable<QuestionImportRow> rows);

        string GetTextTemplate();
    }
}