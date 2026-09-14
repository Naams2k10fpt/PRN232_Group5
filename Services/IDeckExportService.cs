using BusinessObjects;

namespace Services
{
    public interface IDeckExportService
    {
        byte[] ExportDeckToWord(Deck deck, IEnumerable<Question> questions);

        byte[] ExportDeckToPdf(Deck deck, IEnumerable<Question> questions);

        string BuildSafeFileName(string deckName, string extension);
    }
}