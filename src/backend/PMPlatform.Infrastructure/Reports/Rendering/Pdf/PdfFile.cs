using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace PMPlatform.Infrastructure.Reports.Rendering.Pdf;

/// <summary>
/// A PDF 1.7 file assembled object by object (ISO 32000-1 §7.5): numbered indirect objects, Flate-compressed streams, a cross-reference table of
/// byte offsets and a trailer. It writes what it is given and interprets nothing.
/// </summary>
internal sealed class PdfFile
{
    private readonly List<byte[]> _objects = [];

    /// <summary>Reserves an object number to be filled later, for objects that refer to each other.</summary>
    public int Reserve()
    {
        _objects.Add([]);
        return _objects.Count;
    }

    public int Add(string body)
    {
        int number = Reserve();
        Set(number, body);
        return number;
    }

    public void Set(int number, string body) => _objects[number - 1] = Encoding.ASCII.GetBytes(body);

    /// <summary>A stream object, Flate-compressed; <paramref name="dictionary"/> holds the entries besides /Length and /Filter.</summary>
    public int AddStream(string dictionary, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(content);
        }

        byte[] data = compressed.ToArray();
        int number = Reserve();
        byte[] head = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"<< {dictionary} /Filter /FlateDecode /Length {data.Length} >>\nstream\n"));
        byte[] tail = Encoding.ASCII.GetBytes("\nendstream");
        _objects[number - 1] = [.. head, .. data, .. tail];
        return number;
    }

    public byte[] Write(int catalog, int info, byte[] id)
    {
        using MemoryStream pdf = new();
        void Ascii(string text) => pdf.Write(Encoding.ASCII.GetBytes(text));

        Ascii("%PDF-1.7\n");
        pdf.Write([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]);
        List<long> offsets = [];
        for (int i = 0; i < _objects.Count; i++)
        {
            offsets.Add(pdf.Position);
            Ascii(string.Create(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n"));
            pdf.Write(_objects[i]);
            Ascii("\nendobj\n");
        }

        long xref = pdf.Position;
        StringBuilder table = new(string.Create(CultureInfo.InvariantCulture, $"xref\n0 {_objects.Count + 1}\n0000000000 65535 f \n"));
        foreach (long offset in offsets)
        {
            table.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        string hex = Convert.ToHexString(id);
        table.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {_objects.Count + 1} /Root {catalog} 0 R /Info {info} 0 R /ID [<{hex}> <{hex}>] >>\nstartxref\n{xref}\n%%EOF\n");
        Ascii(table.ToString());
        return pdf.ToArray();
    }

    /// <summary>A text string as UTF-16BE with its byte-order mark, in hex (ISO 32000-1 §7.9.2.2): any script, no escaping.</summary>
    public static string TextString(string text) => $"<FEFF{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text))}>";
}
