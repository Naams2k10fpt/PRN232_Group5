using Services;
using BusinessObjects;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace QuizManagement.Tests.UnitTests;

public class DeckExportServiceTests
{
    [Fact]
    public void BuildSafeFileName_UsesFallbackForBlankDeckName()
    {
        var service = new DeckExportService();

        var fileName = service.BuildSafeFileName("   ", ".pdf");

        Assert.Equal("deck.pdf", fileName);
    }

    [Fact]
    public void ExportDeckToPdf_EmbedsUnicodeFontAndToUnicodeMap()
    {
        var service = new DeckExportService();
        var deck = new Deck
        {
            Name = "Bộ đề Tiếng Việt",
            Subject = new Subject { Name = "Lập trình đường phố" }
        };
        var questions = new[]
        {
            new Question
            {
                Id = 1,
                Content = "Câu hỏi có đáp án đúng là gì?",
                QuestionType = 1,
                Explanation = "Giải thích giữ nguyên dấu tiếng Việt.",
                Answers =
                [
                    new Answer { Id = 1, Content = "Đáp án đúng", IsCorrect = true },
                    new Answer { Id = 2, Content = "Đáp án sai" }
                ]
            }
        };

        var pdf = service.ExportDeckToPdf(deck, questions);
        var ascii = Encoding.ASCII.GetString(pdf);

        Assert.StartsWith("%PDF-", ascii);
        Assert.Contains("/BaseFont /DejaVuSans", ascii);
        Assert.Contains("/FontFile2", ascii);
        Assert.Contains("/ToUnicode", ascii);
        Assert.Contains("0110", ascii);
    }

    [Fact]
    public void ExportDeckToWord_StillPreservesVietnameseXmlText()
    {
        var deck = new Deck
        {
            Name = "Bộ đề Tiếng Việt",
            Subject = new Subject { Name = "Dữ liệu" }
        };
        var question = new Question
        {
            Id = 1,
            Content = "Câu hỏi đường phố",
            QuestionType = 1,
            Answers = [new Answer { Id = 1, Content = "Đáp án đúng", IsCorrect = true }]
        };

        var word = new DeckExportService().ExportDeckToWord(deck, [question]);
        using var archive = new ZipArchive(new MemoryStream(word), ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open(), Encoding.UTF8);
        var xml = reader.ReadToEnd();

        Assert.Contains("Bộ đề Tiếng Việt", xml);
        Assert.Contains("Câu hỏi đường phố", xml);
        Assert.Contains("Đáp án đúng", xml);
    }
}
