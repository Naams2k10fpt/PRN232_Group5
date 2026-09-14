using BusinessObjects;
using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace Services
{
    public class DeckExportService : IDeckExportService
    {
        private static readonly Lazy<TrueTypePdfFont> PdfFont = new(LoadPdfFont);

        public byte[] ExportDeckToWord(Deck deck, IEnumerable<Question> questions)
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteEntry(archive, "[Content_Types].xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
</Types>
""");

                WriteEntry(archive, "_rels/.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>
""");

                WriteEntry(archive, "word/document.xml", BuildWordDocument(deck, questions.ToList()));
            }

            return memoryStream.ToArray();
        }

        public byte[] ExportDeckToPdf(Deck deck, IEnumerable<Question> questions)
        {
            var font = PdfFont.Value;
            var lines = BuildPlainTextLines(deck, questions.ToList())
                .SelectMany(line => WrapLine(line, 80))
                .ToList();

            if (lines.Count == 0)
            {
                lines.Add("No content.");
            }

            var encoding = new PdfUnicodeEncoding(font, lines);
            var pages = lines.Chunk(48).ToList();
            var objects = new List<byte[]>
            {
                AsciiBytes("<< /Type /Catalog /Pages 2 0 R >>"),
                Array.Empty<byte>(),
                AsciiBytes("<< /Type /Font /Subtype /Type0 /BaseFont /DejaVuSans /Encoding /Identity-H /DescendantFonts [4 0 R] /ToUnicode 7 0 R >>"),
                AsciiBytes($"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /DejaVuSans /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 5 0 R /CIDToGIDMap 8 0 R /DW 600 {encoding.BuildWidths()} >>"),
                AsciiBytes($"<< /Type /FontDescriptor /FontName /DejaVuSans /Flags 32 /FontBBox [{font.XMin} {font.YMin} {font.XMax} {font.YMax}] /ItalicAngle 0 /Ascent {font.Ascent} /Descent {font.Descent} /CapHeight {font.Ascent} /StemV 80 /FontFile2 6 0 R >>"),
                BuildStreamObject(font.Data, $"/Length1 {font.Data.Length}"),
                BuildStreamObject(Encoding.ASCII.GetBytes(encoding.BuildToUnicodeCMap())),
                BuildStreamObject(encoding.BuildCidToGlyphMap())
            };

            var pageIds = new List<int>();
            foreach (var pageLines in pages)
            {
                var contentStream = BuildPdfContentStream(pageLines, encoding);
                var contentBytes = Encoding.ASCII.GetBytes(contentStream);
                var contentObjectId = objects.Count + 1;
                objects.Add(BuildStreamObject(contentBytes));

                var pageObjectId = objects.Count + 1;
                pageIds.Add(pageObjectId);
                objects.Add(AsciiBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectId} 0 R >>"));
            }

            objects[1] = AsciiBytes($"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] /Count {pageIds.Count} >>");
            return BuildPdf(objects);
        }

        public string BuildSafeFileName(string deckName, string extension)
        {
            var safeName = string.Concat(deckName.Select(ch =>
                Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch)).Trim();

            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "deck";
            }

            return $"{safeName}.{extension.TrimStart('.')}";
        }

        private static string BuildWordDocument(Deck deck, IReadOnlyCollection<Question> questions)
        {
            var builder = new StringBuilder();
            builder.Append("""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
<w:body>
""");

            AppendParagraph(builder, deck.Name, bold: true, size: 32);
            AppendParagraph(builder, $"Subject: {deck.Subject.Name}", bold: true);
            AppendParagraph(builder, $"Questions: {questions.Count}");
            AppendParagraph(builder, string.Empty);

            var index = 1;
            foreach (var question in questions.OrderBy(q => q.Id))
            {
                AppendMultilineParagraph(builder, $"Question {index}: {question.Content}", bold: true);
                AppendParagraph(builder, question.QuestionType == 1 ? "Type: Single choice" : "Type: Multiple choice");

                foreach (var answer in question.Answers.OrderBy(a => a.Id))
                {
                    AppendMultilineParagraph(builder, $"{(answer.IsCorrect ? "[x]" : "[ ]")} {answer.Content}");
                }

                if (!string.IsNullOrWhiteSpace(question.Explanation))
                {
                    AppendMultilineParagraph(builder, $"Explanation: {question.Explanation}");
                }

                AppendParagraph(builder, string.Empty);
                index++;
            }

            builder.Append("""
<w:sectPr>
  <w:pgSz w:w="12240" w:h="15840"/>
  <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="720" w:footer="720" w:gutter="0"/>
</w:sectPr>
</w:body>
</w:document>
""");

            return builder.ToString();
        }

        private static List<string> BuildPlainTextLines(Deck deck, IReadOnlyCollection<Question> questions)
        {
            var lines = new List<string>
            {
                deck.Name,
                $"Subject: {deck.Subject.Name}",
                $"Questions: {questions.Count}",
                string.Empty
            };

            var index = 1;
            foreach (var question in questions.OrderBy(q => q.Id))
            {
                lines.Add($"Question {index}: {question.Content}");
                lines.Add(question.QuestionType == 1 ? "Type: Single choice" : "Type: Multiple choice");

                foreach (var answer in question.Answers.OrderBy(a => a.Id))
                {
                    lines.Add($"{(answer.IsCorrect ? "[x]" : "[ ]")} {answer.Content}");
                }

                if (!string.IsNullOrWhiteSpace(question.Explanation))
                {
                    lines.Add($"Explanation: {question.Explanation}");
                }

                lines.Add(string.Empty);
                index++;
            }

            return lines;
        }

        private static string BuildPdfContentStream(
            IEnumerable<string> lines,
            PdfUnicodeEncoding encoding)
        {
            var builder = new StringBuilder();
            builder.AppendLine("BT");
            builder.AppendLine("/F1 11 Tf");
            builder.AppendLine("50 790 Td");
            builder.AppendLine("14 TL");

            foreach (var line in lines)
            {
                builder.Append(encoding.Encode(line)).AppendLine(" Tj");
                builder.AppendLine("T*");
            }

            builder.AppendLine("ET");
            return builder.ToString();
        }

        private static byte[] BuildPdf(IReadOnlyList<byte[]> objects)
        {
            using var stream = new MemoryStream();
            WriteAscii(stream, "%PDF-1.4\n");

            var offsets = new List<long> { 0 };
            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(stream.Position);
                WriteAscii(stream, $"{i + 1} 0 obj\n");
                stream.Write(objects[i]);
                WriteAscii(stream, "\nendobj\n");
            }

            var xrefOffset = stream.Position;
            WriteAscii(stream, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
            foreach (var offset in offsets.Skip(1))
            {
                WriteAscii(stream, $"{offset.ToString("0000000000", CultureInfo.InvariantCulture)} 00000 n \n");
            }

            WriteAscii(stream, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
            return stream.ToArray();
        }

        private static byte[] BuildStreamObject(byte[] content, string? extraDictionary = null)
        {
            using var stream = new MemoryStream();
            WriteAscii(stream,
                $"<< /Length {content.Length}{(string.IsNullOrWhiteSpace(extraDictionary) ? string.Empty : $" {extraDictionary}")} >>\nstream\n");
            stream.Write(content);
            WriteAscii(stream, "\nendstream");
            return stream.ToArray();
        }

        private static IEnumerable<string> WrapLine(string line, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                yield return string.Empty;
                yield break;
            }

            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = new StringBuilder();

            foreach (var word in words)
            {
                if (current.Length > 0 && current.Length + word.Length + 1 > maxLength)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                if (word.Length > maxLength)
                {
                    if (current.Length > 0)
                    {
                        yield return current.ToString();
                        current.Clear();
                    }

                    for (var i = 0; i < word.Length; i += maxLength)
                    {
                        yield return word.Substring(i, Math.Min(maxLength, word.Length - i));
                    }

                    continue;
                }

                if (current.Length > 0)
                {
                    current.Append(' ');
                }

                current.Append(word);
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
            }
        }

        private static void AppendMultilineParagraph(StringBuilder builder, string text, bool bold = false)
        {
            foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            {
                AppendParagraph(builder, line, bold);
            }
        }

        private static void AppendParagraph(StringBuilder builder, string text, bool bold = false, int size = 22)
        {
            builder.Append("<w:p><w:r>");
            if (bold || size != 22)
            {
                builder.Append("<w:rPr>");
                if (bold)
                {
                    builder.Append("<w:b/>");
                }

                if (size != 22)
                {
                    builder.Append("<w:sz w:val=\"").Append(size).Append("\"/>");
                }

                builder.Append("</w:rPr>");
            }

            builder.Append("<w:t xml:space=\"preserve\">")
                .Append(XmlEscape(text))
                .Append("</w:t></w:r></w:p>");
        }

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            var entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }

        private static void WriteAscii(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static byte[] AsciiBytes(string value) => Encoding.ASCII.GetBytes(value);

        private static TrueTypePdfFont LoadPdfFont()
        {
            using var stream = typeof(DeckExportService).Assembly.GetManifestResourceStream(
                "Services.Assets.Fonts.DejaVuSans.ttf")
                ?? throw new InvalidOperationException("Embedded DejaVu Sans PDF font was not found.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return new TrueTypePdfFont(memory.ToArray());
        }

        private static string XmlEscape(string value)
            => SecurityElement.Escape(value) ?? string.Empty;

    }
}
