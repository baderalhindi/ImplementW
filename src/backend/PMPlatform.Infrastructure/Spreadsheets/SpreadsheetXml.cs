using System.IO.Compression;
using System.Security;
using System.Text;

namespace PMPlatform.Infrastructure.Spreadsheets;

/// <summary>
/// The Office Open XML plumbing the platform's workbooks share (ADR-005): written directly, with no spreadsheet library. The report
/// export (FG-02) and the migration import templates (TASK-102) are both built from it.
/// </summary>
internal static class SpreadsheetXml
{
    public const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public const string PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    public const string MediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static void Part(ZipArchive zip, string name, string xml)
    {
        ArgumentNullException.ThrowIfNull(zip);
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xml);
        stream.Write(bytes);
    }

    /// <summary>XML text: escaped, and without the characters XML 1.0 cannot hold.</summary>
    public static string Escape(string text) =>
        SecurityElement.Escape(new string([.. text.Where(c => c is '\t' or '\n' or '\r' || (!char.IsControl(c) && c != '￾' && c != '￿'))])) ?? string.Empty;

    /// <summary>A zero-based column index as its letters: 0 is A, 26 is AA.</summary>
    public static string ColumnName(int index)
    {
        string name = string.Empty;
        for (int n = index + 1; n > 0; n = (n - 1) / 26)
        {
            name = (char)('A' + ((n - 1) % 26)) + name;
        }

        return name;
    }

    /// <summary>The zero-based column index of a cell reference such as <c>AB12</c>; -1 when it has no letters.</summary>
    public static int ColumnIndex(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        int index = 0;
        int letters = 0;
        foreach (char c in reference)
        {
            if (c is >= 'A' and <= 'Z')
            {
                index = (index * 26) + c - 'A' + 1;
                letters++;
            }
            else
            {
                break;
            }
        }

        return letters is 0 or > 3 ? -1 : index - 1;
    }
}
