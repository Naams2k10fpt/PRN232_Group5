using System.Text;

namespace Services;

internal sealed class PdfUnicodeEncoding
{
    private readonly Dictionary<Rune, ushort> _characterIds;
    private readonly List<(ushort CharacterId, Rune Rune, ushort GlyphId, int Width)> _mappings;

    public PdfUnicodeEncoding(TrueTypePdfFont font, IEnumerable<string> lines)
    {
        _characterIds = new Dictionary<Rune, ushort>();
        _mappings = [];

        foreach (var rune in lines.SelectMany(line => line.EnumerateRunes()).Distinct())
        {
            if (_mappings.Count == ushort.MaxValue - 1)
                throw new InvalidDataException("PDF contains too many distinct Unicode characters.");

            var characterId = checked((ushort)(_mappings.Count + 1));
            var glyphId = font.GetGlyphId(rune.Value);
            _characterIds[rune] = characterId;
            _mappings.Add((characterId, rune, glyphId, font.GetPdfWidth(glyphId)));
        }
    }

    public string Encode(string value)
    {
        var builder = new StringBuilder("<");
        foreach (var rune in value.EnumerateRunes())
            builder.Append(_characterIds[rune].ToString("X4"));
        return builder.Append('>').ToString();
    }

    public string BuildWidths()
        => _mappings.Count == 0
            ? string.Empty
            : $"/W [1 [{string.Join(' ', _mappings.Select(mapping => mapping.Width))}]]";

    public byte[] BuildCidToGlyphMap()
    {
        var bytes = new byte[(_mappings.Count + 1) * 2];
        foreach (var mapping in _mappings)
        {
            bytes[mapping.CharacterId * 2] = (byte)(mapping.GlyphId >> 8);
            bytes[mapping.CharacterId * 2 + 1] = (byte)mapping.GlyphId;
        }
        return bytes;
    }

    public string BuildToUnicodeCMap()
    {
        var builder = new StringBuilder("""
            /CIDInit /ProcSet findresource begin
            12 dict begin
            begincmap
            /CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def
            /CMapName /Adobe-Identity-UCS def
            /CMapType 2 def
            1 begincodespacerange
            <0000> <FFFF>
            endcodespacerange
            """);
        builder.AppendLine();

        foreach (var chunk in _mappings.Chunk(100))
        {
            builder.Append(chunk.Length).AppendLine(" beginbfchar");
            foreach (var mapping in chunk)
            {
                var unicodeHex = Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(mapping.Rune.ToString()));
                builder.Append('<').Append(mapping.CharacterId.ToString("X4"))
                    .Append("> <").Append(unicodeHex).AppendLine(">");
            }
            builder.AppendLine("endbfchar");
        }

        builder.AppendLine("endcmap");
        builder.AppendLine("CMapName currentdict /CMap defineresource pop");
        builder.AppendLine("end");
        builder.AppendLine("end");
        return builder.ToString();
    }
}
