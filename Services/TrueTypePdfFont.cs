using System.Buffers.Binary;

namespace Services;

internal sealed class TrueTypePdfFont
{
    private readonly byte[] _data;
    private readonly ushort[] _advanceWidths;
    private readonly int _cmapOffset;
    private readonly ushort _cmapFormat;

    public TrueTypePdfFont(byte[] data)
    {
        _data = data;
        var tables = ReadTables();
        var head = RequireTable(tables, "head");
        var hhea = RequireTable(tables, "hhea");
        var hmtx = RequireTable(tables, "hmtx");
        var maxp = RequireTable(tables, "maxp");
        var cmap = RequireTable(tables, "cmap");

        UnitsPerEm = ReadUInt16(head + 18);
        XMin = Scale(ReadInt16(head + 36));
        YMin = Scale(ReadInt16(head + 38));
        XMax = Scale(ReadInt16(head + 40));
        YMax = Scale(ReadInt16(head + 42));
        Ascent = Scale(ReadInt16(hhea + 4));
        Descent = Scale(ReadInt16(hhea + 6));

        var glyphCount = ReadUInt16(maxp + 4);
        var metricCount = ReadUInt16(hhea + 34);
        if (metricCount == 0 || metricCount > glyphCount)
            throw new InvalidDataException("Embedded TrueType font has invalid horizontal metrics.");

        _advanceWidths = new ushort[glyphCount];
        for (var glyph = 0; glyph < metricCount; glyph++)
            _advanceWidths[glyph] = ReadUInt16(hmtx + glyph * 4);
        for (var glyph = metricCount; glyph < glyphCount; glyph++)
            _advanceWidths[glyph] = _advanceWidths[metricCount - 1];

        (_cmapOffset, _cmapFormat) = SelectCmap(cmap);
    }

    public byte[] Data => _data;
    public int UnitsPerEm { get; }
    public int Ascent { get; }
    public int Descent { get; }
    public int XMin { get; }
    public int YMin { get; }
    public int XMax { get; }
    public int YMax { get; }

    public ushort GetGlyphId(int unicodeScalar)
        => _cmapFormat == 12
            ? GetFormat12Glyph(unicodeScalar)
            : GetFormat4Glyph(unicodeScalar);

    public int GetPdfWidth(ushort glyphId)
    {
        var width = glyphId < _advanceWidths.Length
            ? _advanceWidths[glyphId]
            : _advanceWidths[0];
        return (int)Math.Round(width * 1000d / UnitsPerEm);
    }

    private Dictionary<string, int> ReadTables()
    {
        var tableCount = ReadUInt16(4);
        var tables = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < tableCount; index++)
        {
            var record = 12 + index * 16;
            var tag = System.Text.Encoding.ASCII.GetString(_data, record, 4);
            tables[tag] = checked((int)ReadUInt32(record + 8));
        }
        return tables;
    }

    private static int RequireTable(IReadOnlyDictionary<string, int> tables, string name)
        => tables.TryGetValue(name, out var offset)
            ? offset
            : throw new InvalidDataException($"Embedded TrueType font is missing the {name} table.");

    private (int Offset, ushort Format) SelectCmap(int cmap)
    {
        var recordCount = ReadUInt16(cmap + 2);
        var bestScore = -1;
        var bestOffset = 0;
        ushort bestFormat = 0;

        for (var index = 0; index < recordCount; index++)
        {
            var record = cmap + 4 + index * 8;
            var platform = ReadUInt16(record);
            var encoding = ReadUInt16(record + 2);
            var subtable = checked(cmap + (int)ReadUInt32(record + 4));
            var format = ReadUInt16(subtable);
            var score = format switch
            {
                12 when platform == 3 && encoding == 10 => 100,
                12 when platform == 0 => 90,
                4 when platform == 3 && encoding == 1 => 80,
                4 when platform == 0 => 70,
                _ => -1
            };
            if (score > bestScore)
            {
                bestScore = score;
                bestOffset = subtable;
                bestFormat = format;
            }
        }

        if (bestScore < 0)
            throw new InvalidDataException("Embedded TrueType font has no supported Unicode cmap.");
        return (bestOffset, bestFormat);
    }

    private ushort GetFormat12Glyph(int unicodeScalar)
    {
        var groupCount = checked((int)ReadUInt32(_cmapOffset + 12));
        var low = 0;
        var high = groupCount - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var group = _cmapOffset + 16 + middle * 12;
            var start = ReadUInt32(group);
            var end = ReadUInt32(group + 4);
            if ((uint)unicodeScalar < start)
                high = middle - 1;
            else if ((uint)unicodeScalar > end)
                low = middle + 1;
            else
                return checked((ushort)(ReadUInt32(group + 8) + (uint)unicodeScalar - start));
        }
        return 0;
    }

    private ushort GetFormat4Glyph(int unicodeScalar)
    {
        if (unicodeScalar > ushort.MaxValue)
            return 0;

        var segmentCount = ReadUInt16(_cmapOffset + 6) / 2;
        var endCodes = _cmapOffset + 14;
        var startCodes = endCodes + segmentCount * 2 + 2;
        var deltas = startCodes + segmentCount * 2;
        var rangeOffsets = deltas + segmentCount * 2;

        for (var index = 0; index < segmentCount; index++)
        {
            var end = ReadUInt16(endCodes + index * 2);
            if (unicodeScalar > end)
                continue;

            var start = ReadUInt16(startCodes + index * 2);
            if (unicodeScalar < start)
                return 0;

            var delta = ReadInt16(deltas + index * 2);
            var rangeOffsetAddress = rangeOffsets + index * 2;
            var rangeOffset = ReadUInt16(rangeOffsetAddress);
            if (rangeOffset == 0)
                return (ushort)((unicodeScalar + delta) & 0xFFFF);

            var glyphAddress = rangeOffsetAddress + rangeOffset + (unicodeScalar - start) * 2;
            var glyph = ReadUInt16(glyphAddress);
            return glyph == 0 ? (ushort)0 : (ushort)((glyph + delta) & 0xFFFF);
        }

        return 0;
    }

    private int Scale(short value) => (int)Math.Round(value * 1000d / UnitsPerEm);
    private ushort ReadUInt16(int offset) => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(offset, 2));
    private short ReadInt16(int offset) => BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(offset, 2));
    private uint ReadUInt32(int offset) => BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(offset, 4));
}
