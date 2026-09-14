using System.IO.Compression;
using System.Text;
using System.Xml;
using Services;
using Xunit;

namespace QuizManagement.Tests.ServiceTests;

public class QuestionImportLimitsTests
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private readonly QuestionImportService _service = new();

    [Fact]
    public void ParseExcel_RejectsWrongSignature()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not an xlsx"));

        Assert.Throws<InvalidDataException>(() => _service.ParseExcel(stream));
    }

    [Fact]
    public void ParseExcel_RejectsOversizedUploadBeforeOpeningZip()
    {
        using var stream = new MemoryStream(new byte[QuestionImportLimits.MaxUploadBytes + 1]);

        Assert.Throws<InvalidDataException>(() => _service.ParseExcel(stream));
    }

    [Fact]
    public void ParseExcel_RejectsTooManyZipEntries()
    {
        using var stream = CreateZip(archive =>
        {
            for (var i = 0; i <= QuestionImportLimits.MaxZipEntries; i++)
                WriteEntry(archive, $"entry-{i}.xml", string.Empty);
        });

        Assert.Throws<InvalidDataException>(() => _service.ParseExcel(stream));
    }

    [Fact]
    public void ParseExcel_RejectsOversizedDecompressedEntry()
    {
        using var stream = CreateZip(archive => WriteEntry(
            archive,
            "xl/worksheets/sheet1.xml",
            new string('x', checked((int)QuestionImportLimits.MaxEntryBytes + 1))));

        Assert.Throws<InvalidDataException>(() => _service.ParseExcel(stream));
    }

    [Fact]
    public void ParseExcel_RejectsOversizedTotalDecompressedContent()
    {
        var entryLength = checked((int)(QuestionImportLimits.MaxTotalUncompressedBytes / 3 + 1));
        var content = new string('x', entryLength);
        using var stream = CreateZip(archive =>
        {
            WriteEntry(archive, "xl/worksheets/sheet1.xml", content);
            WriteEntry(archive, "part-2.xml", content);
            WriteEntry(archive, "part-3.xml", content);
        });

        Assert.Throws<InvalidDataException>(() => _service.ParseExcel(stream));
    }

    [Fact]
    public void ParseExcel_RejectsTooManyRows()
    {
        var rows = string.Concat(Enumerable.Repeat("<row />", QuestionImportLimits.MaxRows + 1));
        using var stream = CreateWorksheet($"<sheetData>{rows}</sheetData>");

        Assert.Throws<InvalidDataException>(() => _service.ParseExcel(stream));
    }

    [Fact]
    public void ParseExcel_RejectsTooManyCellsInOneRow()
    {
        var cells = string.Concat(Enumerable.Repeat(
            "<c r=\"A1\"><v>1</v></c>",
            QuestionImportLimits.MaxCellsPerRow + 1));
        using var stream = CreateWorksheet($"<sheetData><row>{cells}</row></sheetData>");

        Assert.Throws<InvalidDataException>(() => _service.ParseExcel(stream));
    }

    [Fact]
    public void ParseExcel_RejectsDtdProcessing()
    {
        var xml = $"""
            <!DOCTYPE worksheet [<!ENTITY payload "unsafe">]>
            <worksheet xmlns="{SpreadsheetNs}"><sheetData /></worksheet>
            """;
        using var stream = CreateZip(archive =>
            WriteEntry(archive, "xl/worksheets/sheet1.xml", xml));

        Assert.Throws<XmlException>(() => _service.ParseExcel(stream));
    }

    [Fact]
    public void ParseExcel_ParsesOrdinarySupportedWorkbook()
    {
        using var stream = CreateWorksheet("""
            <sheetData>
              <row>
                <c r="A1" t="inlineStr"><is><t>Question</t></is></c>
                <c r="B1" t="inlineStr"><is><t>Type</t></is></c>
                <c r="C1" t="inlineStr"><is><t>Answer1</t></is></c>
                <c r="D1" t="inlineStr"><is><t>Correct1</t></is></c>
                <c r="E1" t="inlineStr"><is><t>Answer2</t></is></c>
                <c r="F1" t="inlineStr"><is><t>Correct2</t></is></c>
              </row>
              <row>
                <c r="A2" t="inlineStr"><is><t>2 + 2?</t></is></c>
                <c r="B2" t="inlineStr"><is><t>single</t></is></c>
                <c r="C2" t="inlineStr"><is><t>4</t></is></c>
                <c r="D2" t="inlineStr"><is><t>true</t></is></c>
                <c r="E2" t="inlineStr"><is><t>3</t></is></c>
                <c r="F2" t="inlineStr"><is><t>false</t></is></c>
              </row>
            </sheetData>
            """);

        var preview = _service.ParseExcel(stream);

        var row = Assert.Single(preview.ValidRows);
        Assert.Equal("2 + 2?", row.Content);
        Assert.Equal(2, row.Answers.Count);
        Assert.Empty(preview.Errors);
    }

    [Fact]
    public void ParseText_RejectsOversizedTextAndTooManyRows()
    {
        Assert.Throws<InvalidDataException>(() =>
            _service.ParseText(new string('x', QuestionImportLimits.MaxTextCharacters + 1)));

        var rows = string.Join("\n\n", Enumerable.Repeat("Question", QuestionImportLimits.MaxRows + 1));
        Assert.Throws<InvalidDataException>(() => _service.ParseText(rows));
    }

    private static MemoryStream CreateWorksheet(string content)
        => CreateZip(archive => WriteEntry(
            archive,
            "xl/worksheets/sheet1.xml",
            $"<worksheet xmlns=\"{SpreadsheetNs}\">{content}</worksheet>"));

    private static MemoryStream CreateZip(Action<ZipArchive> write)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            write(archive);
        stream.Position = 0;
        return stream;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.SmallestSize);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
