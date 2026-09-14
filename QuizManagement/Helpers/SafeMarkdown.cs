using Microsoft.AspNetCore.Html;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace QuizManagement.Helpers
{
    public static class SafeMarkdown
    {
        public static IHtmlContent ToHtml(string? markdown)
            => new HtmlString(Render(markdown));

        public static IHtmlContent ToInlineHtml(string? markdown)
            => new HtmlString(RenderInline(markdown ?? string.Empty));

        private static string Render(string? markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return string.Empty;
            }

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var builder = new StringBuilder();
            var paragraphLines = new List<string>();
            var inList = false;

            void FlushParagraph()
            {
                if (paragraphLines.Count == 0)
                {
                    return;
                }

                builder.Append("<p>")
                    .Append(RenderInline(string.Join(" ", paragraphLines)))
                    .Append("</p>");
                paragraphLines.Clear();
            }

            void CloseList()
            {
                if (!inList)
                {
                    return;
                }

                builder.Append("</ul>");
                inList = false;
            }

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    FlushParagraph();
                    CloseList();
                    continue;
                }

                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("### ", StringComparison.Ordinal))
                {
                    FlushParagraph();
                    CloseList();
                    builder.Append("<h4>").Append(RenderInline(trimmed[4..].Trim())).Append("</h4>");
                    continue;
                }

                if (trimmed.StartsWith("## ", StringComparison.Ordinal))
                {
                    FlushParagraph();
                    CloseList();
                    builder.Append("<h3>").Append(RenderInline(trimmed[3..].Trim())).Append("</h3>");
                    continue;
                }

                if (trimmed.StartsWith("# ", StringComparison.Ordinal))
                {
                    FlushParagraph();
                    CloseList();
                    builder.Append("<h2>").Append(RenderInline(trimmed[2..].Trim())).Append("</h2>");
                    continue;
                }

                if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
                {
                    FlushParagraph();
                    if (!inList)
                    {
                        builder.Append("<ul>");
                        inList = true;
                    }

                    builder.Append("<li>").Append(RenderInline(trimmed[2..].Trim())).Append("</li>");
                    continue;
                }

                paragraphLines.Add(trimmed);
            }

            FlushParagraph();
            CloseList();
            return builder.ToString();
        }

        private static string RenderInline(string value)
        {
            var encoded = HtmlEncoder.Default.Encode(value);

            encoded = Regex.Replace(encoded, @"`([^`]+)`", "<code>$1</code>");
            encoded = Regex.Replace(encoded, @"\*\*([^*]+)\*\*", "<strong>$1</strong>");
            encoded = Regex.Replace(encoded, @"(?<!\*)\*([^*]+)\*(?!\*)", "<em>$1</em>");
            encoded = Regex.Replace(encoded, @"\[(?<text>[^\]]+)\]\((?<url>https?://[^)\s]+|mailto:[^)\s]+)\)",
                match =>
                {
                    var label = match.Groups["text"].Value;
                    var url = HtmlEncoder.Default.Encode(match.Groups["url"].Value);
                    return $"<a href=\"{url}\" target=\"_blank\" rel=\"noopener noreferrer\">{label}</a>";
                });

            return encoded.Replace("\n", "<br>", StringComparison.Ordinal);
        }
    }
}