using System.Buffers.Binary;

namespace PMPlatform.Infrastructure.Reports.Rendering.Pdf;

/// <summary>
/// The TrueType font a PDF output embeds (FG-02 §9.1: bilingual/RTL support): its character map, advance widths and vertical metrics, and a subset
/// of it for one document — every glyph outline the document does not use is emptied, glyph ids unchanged, so the PDF names glyphs by id
/// (Identity-H) and carries only what it draws.
/// </summary>
internal sealed class TrueTypeFont
{
    private readonly byte[] _data;
    private readonly Dictionary<string, (int Offset, int Length)> _tables = new(StringComparer.Ordinal);
    private readonly Dictionary<int, ushort> _cmap = [];
    private readonly ushort[] _advances;
    private readonly int[] _loca;

    public TrueTypeFont(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        int tables = U16(4);
        for (int i = 0; i < tables; i++)
        {
            int record = 12 + (16 * i);
            _tables[System.Text.Encoding.ASCII.GetString(_data, record, 4)] = ((int)U32(record + 8), (int)U32(record + 12));
        }

        int head = Table("head").Offset;
        UnitsPerEm = U16(head + 18);
        BoundingBox = [S16(head + 36), S16(head + 38), S16(head + 40), S16(head + 42)];
        bool longLoca = S16(head + 50) == 1;
        int hhea = Table("hhea").Offset;
        Ascent = S16(hhea + 4);
        Descent = S16(hhea + 6);
        int metrics = U16(hhea + 34);
        GlyphCount = U16(Table("maxp").Offset + 4);

        int hmtx = Table("hmtx").Offset;
        _advances = new ushort[GlyphCount];
        for (int g = 0; g < GlyphCount; g++)
        {
            _advances[g] = U16(hmtx + (4 * Math.Min(g, metrics - 1)));
        }

        int loca = Table("loca").Offset;
        _loca = new int[GlyphCount + 1];
        for (int g = 0; g <= GlyphCount; g++)
        {
            _loca[g] = longLoca ? (int)U32(loca + (4 * g)) : U16(loca + (2 * g)) * 2;
        }

        ReadCharacterMap();
    }

    public int UnitsPerEm { get; }

    public int Ascent { get; }

    public int Descent { get; }

    public int GlyphCount { get; }

    public IReadOnlyList<int> BoundingBox { get; }

    /// <summary>The glyph of a character; 0 (.notdef) when the font has none.</summary>
    public ushort GlyphOf(int codePoint) => _cmap.GetValueOrDefault(codePoint);

    public bool Has(int codePoint) => _cmap.ContainsKey(codePoint);

    /// <summary>A glyph's advance in font units.</summary>
    public int Advance(ushort glyph) => _advances[glyph];

    /// <summary>
    /// The font with only <paramref name="glyphs"/> (and the glyphs they are composed of, and .notdef) keeping an outline; tables a PDF does not
    /// need are left out. Glyph ids and metrics are unchanged.
    /// </summary>
    public byte[] Subset(IReadOnlySet<ushort> glyphs)
    {
        ArgumentNullException.ThrowIfNull(glyphs);
        HashSet<ushort> kept = [0];
        Stack<ushort> pending = new(glyphs);
        while (pending.TryPop(out ushort glyph))
        {
            if (glyph < GlyphCount && kept.Add(glyph))
            {
                foreach (ushort component in Components(glyph))
                {
                    pending.Push(component);
                }
            }
        }

        int glyf = Table("glyf").Offset;
        using MemoryStream outlines = new();
        int[] loca = new int[GlyphCount + 1];
        for (int g = 0; g < GlyphCount; g++)
        {
            loca[g] = (int)outlines.Length;
            if (kept.Contains((ushort)g) && _loca[g + 1] > _loca[g])
            {
                outlines.Write(_data, glyf + _loca[g], _loca[g + 1] - _loca[g]);
                while (outlines.Length % 4 != 0)
                {
                    outlines.WriteByte(0);
                }
            }
        }

        loca[GlyphCount] = (int)outlines.Length;
        byte[] locaTable = new byte[4 * (GlyphCount + 1)];
        for (int g = 0; g <= GlyphCount; g++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(locaTable.AsSpan(4 * g), (uint)loca[g]);
        }

        byte[] head = Copy("head");
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(50), 1);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(8), 0);

        SortedDictionary<string, byte[]> tables = new(StringComparer.Ordinal)
        {
            ["head"] = head,
            ["hhea"] = Copy("hhea"),
            ["hmtx"] = Copy("hmtx"),
            ["maxp"] = Copy("maxp"),
            ["loca"] = locaTable,
            ["glyf"] = outlines.ToArray(),
        };
        foreach (string optional in new[] { "cvt ", "fpgm", "prep", "OS/2" })
        {
            if (_tables.ContainsKey(optional))
            {
                tables[optional] = Copy(optional);
            }
        }

        return Assemble(tables);
    }

    private static byte[] Assemble(SortedDictionary<string, byte[]> tables)
    {
        int count = tables.Count;
        int power = 1 << (int)Math.Floor(Math.Log2(count));
        using MemoryStream font = new();
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt32BigEndian(header, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(header[4..], (ushort)count);
        BinaryPrimitives.WriteUInt16BigEndian(header[6..], (ushort)(power * 16));
        BinaryPrimitives.WriteUInt16BigEndian(header[8..], (ushort)Math.Log2(power));
        BinaryPrimitives.WriteUInt16BigEndian(header[10..], (ushort)((count * 16) - (power * 16)));
        font.Write(header);

        int offset = 12 + (16 * count);
        byte[] directory = new byte[16 * count];
        int i = 0;
        foreach ((string tag, byte[] table) in tables)
        {
            System.Text.Encoding.ASCII.GetBytes(tag).CopyTo(directory, 16 * i);
            BinaryPrimitives.WriteUInt32BigEndian(directory.AsSpan((16 * i) + 4), Checksum(table));
            BinaryPrimitives.WriteUInt32BigEndian(directory.AsSpan((16 * i) + 8), (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(directory.AsSpan((16 * i) + 12), (uint)table.Length);
            offset += (table.Length + 3) & ~3;
            i++;
        }

        font.Write(directory);
        foreach (byte[] table in tables.Values)
        {
            font.Write(table);
            for (int pad = table.Length; pad % 4 != 0; pad++)
            {
                font.WriteByte(0);
            }
        }

        return font.ToArray();
    }

    private static uint Checksum(byte[] table)
    {
        uint sum = 0;
        Span<byte> word = stackalloc byte[4];
        for (int i = 0; i < table.Length; i += 4)
        {
            word.Clear();
            table.AsSpan(i, Math.Min(4, table.Length - i)).CopyTo(word);
            sum += BinaryPrimitives.ReadUInt32BigEndian(word);
        }

        return sum;
    }

    /// <summary>The glyphs a composite glyph is built from (its components); none for a simple glyph.</summary>
    private IEnumerable<ushort> Components(ushort glyph)
    {
        if (_loca[glyph + 1] <= _loca[glyph])
        {
            yield break;
        }

        int at = Table("glyf").Offset + _loca[glyph];
        if (S16(at) >= 0)
        {
            yield break;
        }

        const int ArgsAreWords = 0x0001;
        const int HasScale = 0x0008;
        const int MoreComponents = 0x0020;
        const int HasXyScale = 0x0040;
        const int HasTwoByTwo = 0x0080;
        at += 10;
        int flags;
        do
        {
            flags = U16(at);
            yield return U16(at + 2);
            at += 4 + ((flags & ArgsAreWords) != 0 ? 4 : 2);
            at += (flags & HasScale) != 0 ? 2 : (flags & HasXyScale) != 0 ? 4 : (flags & HasTwoByTwo) != 0 ? 8 : 0;
        }
        while ((flags & MoreComponents) != 0);
    }

    /// <summary>The Windows Unicode map: format 12 (full repertoire) when present, else format 4 (the Basic Multilingual Plane).</summary>
    private void ReadCharacterMap()
    {
        int cmap = Table("cmap").Offset;
        int subtables = U16(cmap + 2);
        int? format4 = null;
        int? format12 = null;
        for (int i = 0; i < subtables; i++)
        {
            int record = cmap + 4 + (8 * i);
            int platform = U16(record);
            int encoding = U16(record + 2);
            int at = cmap + (int)U32(record + 4);
            if (platform == 3 && encoding == 10 && U16(at) == 12)
            {
                format12 = at;
            }
            else if (platform == 3 && encoding == 1 && U16(at) == 4)
            {
                format4 = at;
            }
        }

        if (format12 is { } table12)
        {
            uint groups = U32(table12 + 12);
            for (int g = 0; g < groups; g++)
            {
                int group = table12 + 16 + (12 * g);
                uint start = U32(group);
                uint end = U32(group + 4);
                uint glyph = U32(group + 8);
                for (uint c = start; c <= end && c <= 0x10FFFF; c++)
                {
                    _cmap[(int)c] = (ushort)(glyph + (c - start));
                }
            }

            return;
        }

        int table = format4 ?? throw new InvalidOperationException("The font has no Unicode character map.");
        int segments = U16(table + 6) / 2;
        int ends = table + 14;
        int starts = ends + (2 * segments) + 2;
        int deltas = starts + (2 * segments);
        int ranges = deltas + (2 * segments);
        for (int s = 0; s < segments; s++)
        {
            int end = U16(ends + (2 * s));
            int start = U16(starts + (2 * s));
            short delta = S16(deltas + (2 * s));
            int range = U16(ranges + (2 * s));
            for (int c = start; c <= end && c != 0xFFFF; c++)
            {
                int glyph = range == 0
                    ? (c + delta) & 0xFFFF
                    : U16(ranges + (2 * s) + range + (2 * (c - start))) is var g and not 0 ? (g + delta) & 0xFFFF : 0;
                if (glyph != 0)
                {
                    _cmap[c] = (ushort)glyph;
                }
            }
        }
    }

    private (int Offset, int Length) Table(string tag) =>
        _tables.TryGetValue(tag, out (int Offset, int Length) table) ? table : throw new InvalidOperationException($"The font has no {tag} table.");

    private byte[] Copy(string tag)
    {
        (int offset, int length) = Table(tag);
        return _data.AsSpan(offset, length).ToArray();
    }

    private ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(at));

    private short S16(int at) => BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(at));

    private uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(at));
}
