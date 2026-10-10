using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PMPlatform.Application.Features.Reports;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;
using PMPlatform.Infrastructure.Reports.Rendering.Pdf;

namespace PMPlatform.Infrastructure.Reports.Rendering;

/// <summary>
/// PDF (FG-02 §9.1; ADR-005): the formal printable output — A4 landscape, the title, the context (parameters, Generated At, each source's semantic
/// state, freshness, as-of and coverage, the classification), the table with its header row repeated on every page, and on every page a footer with
/// the page number of the total, Generated At and the classification marking. Arabic outputs are laid out right to left, columns from the right,
/// with Arabic shaped (TBC-RPT-06's branding and watermark are not applied: none is decided). Text is drawn in DejaVu Sans, embedded as a subset of
/// the glyphs the document uses, so every reader shows the same characters and can copy them back (ToUnicode).
/// </summary>
internal sealed class PdfReportRenderer : IReportRenderer
{
    private const double PageWidth = 842;
    private const double PageHeight = 595;
    private const double Margin = 32;
    private const double TitleSize = 13;
    private const double TextSize = 7.5;
    private const double FooterSize = 7;
    private const double Padding = 3;
    private const double RowHeight = TextSize * 1.9;
    private const double MaxColumnWidth = 220;
    private const double MinColumnWidth = 40;
    private const string FontName = "AAAAAA+DejaVuSans";

    private static readonly Lazy<TrueTypeFont> Font = new(() => new TrueTypeFont(FontResource.DejaVuSans()));

    public ReportExportFormat Format => ReportExportFormat.Pdf;

    public RenderedReport Render(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Layout layout = new(Font.Value, document.Language == Language.Ar);
        double[] widths = layout.ColumnWidths(document);
        List<IReadOnlyList<IReadOnlyList<ReportDocumentCell>>> pages = Paginate(document);
        List<string> contents = [.. pages.Select((rows, index) => layout.Page(document, widths, rows, index, pages.Count))];
        return new RenderedReport(Write(document, layout, contents), "application/pdf", "pdf", 0);
    }

    /// <summary>The rows each page holds: the first page gives room to the context; every page to its header row and footer.</summary>
    private static List<IReadOnlyList<IReadOnlyList<ReportDocumentCell>>> Paginate(ReportDocument document)
    {
        double table = PageHeight - (2 * Margin) - (TitleSize * 2) - RowHeight - (FooterSize * 3);
        int first = Math.Max(1, (int)((table - Layout.ContextHeight(document)) / RowHeight));
        int rest = Math.Max(1, (int)(table / RowHeight));
        List<IReadOnlyList<IReadOnlyList<ReportDocumentCell>>> pages = [[.. document.Rows.Take(first)]];
        for (int at = first; at < document.Rows.Count; at += rest)
        {
            pages.Add([.. document.Rows.Skip(at).Take(rest)]);
        }

        return pages;
    }

    private static byte[] Write(ReportDocument document, Layout layout, List<string> contents)
    {
        PdfFile pdf = new();
        int pages = pdf.Reserve();
        int font = pdf.Reserve();
        List<int> pageObjects = [];
        foreach (string content in contents)
        {
            int stream = pdf.AddStream(string.Empty, Encoding.ASCII.GetBytes(content));
            pageObjects.Add(pdf.Add(string.Create(CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent {pages} 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 {font} 0 R >> >> /Contents {stream} 0 R >>")));
        }

        pdf.Set(pages, string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{string.Join(' ', pageObjects.Select(p => $"{p} 0 R"))}] /Count {pageObjects.Count} >>"));

        // The font: Type 0 over a CIDFontType2 that names glyphs by id, the subset embedded, and a ToUnicode map for copying text back.
        TrueTypeFont face = layout.Face;
        byte[] subset = face.Subset(layout.UsedGlyphs);
        int file = pdf.AddStream(string.Create(CultureInfo.InvariantCulture, $"/Length1 {subset.Length}"), subset);
        double scale = 1000.0 / face.UnitsPerEm;
        int descriptor = pdf.Add(string.Create(CultureInfo.InvariantCulture,
            $"<< /Type /FontDescriptor /FontName /{FontName} /Flags 32 /FontBBox [{string.Join(' ', face.BoundingBox.Select(v => Math.Round(v * scale)))}] /ItalicAngle 0 /Ascent {Math.Round(face.Ascent * scale)} /Descent {Math.Round(face.Descent * scale)} /CapHeight {Math.Round(face.Ascent * scale * 0.7)} /StemV 80 /FontFile2 {file} 0 R >>"));
        string widths = string.Join(' ', layout.UsedGlyphs.Order().Select(g => string.Create(CultureInfo.InvariantCulture, $"{g} [{Math.Round(face.Advance(g) * scale)}]")));
        int cidFont = pdf.Add(string.Create(CultureInfo.InvariantCulture,
            $"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /{FontName} /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor {descriptor} 0 R /W [{widths}] /CIDToGIDMap /Identity >>"));
        int toUnicode = pdf.AddStream(string.Empty, Encoding.ASCII.GetBytes(layout.ToUnicode()));
        pdf.Set(font, string.Create(CultureInfo.InvariantCulture,
            $"<< /Type /Font /Subtype /Type0 /BaseFont /{FontName} /Encoding /Identity-H /DescendantFonts [{cidFont} 0 R] /ToUnicode {toUnicode} 0 R >>"));

        string lang = document.Language == Language.Ar ? "ar" : "en";
        string direction = document.Language == Language.Ar ? " /ViewerPreferences << /Direction /R2L >>" : string.Empty;
        int catalog = pdf.Add($"<< /Type /Catalog /Pages {pages} 0 R /Lang ({lang}){direction} >>");
        string created = document.GeneratedAt.UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        int info = pdf.Add($"<< /Title {PdfFile.TextString(document.Title)} /Producer (PMPlatform FG-02) /CreationDate (D:{created}Z) >>");
        byte[] id = SHA256.HashData(Encoding.UTF8.GetBytes($"{document.Title}|{created}|{document.Rows.Count}"))[..16];
        return pdf.Write(catalog, info, id);
    }

    /// <summary>The page geometry and the text drawn on it, recording every glyph used and the characters it stands for.</summary>
    private sealed class Layout(TrueTypeFont face, bool rightToLeft)
    {
        private readonly Dictionary<ushort, string> _glyphText = [];

        public TrueTypeFont Face { get; } = face;

        public HashSet<ushort> UsedGlyphs { get; } = [];

        public static double ContextHeight(ReportDocument document) => (document.Context.Count + document.Notes.Count + 1) * TextSize * 1.5;

        /// <summary>Each column's width: its widest value, within bounds, scaled down together when the page is too narrow.</summary>
        public double[] ColumnWidths(ReportDocument document)
        {
            double[] widths = [.. document.Columns.Select((column, i) => Math.Clamp(
                Math.Max(Measure(column.Header, TextSize), document.Rows.Select(r => Measure(r[i].Text, TextSize)).DefaultIfEmpty(0).Max()) + (2 * Padding),
                MinColumnWidth, MaxColumnWidth))];
            double available = PageWidth - (2 * Margin);
            double total = widths.Sum();
            return total <= available ? widths : [.. widths.Select(w => Math.Max(MinColumnWidth * 0.75, w * available / total))];
        }

        public string Page(ReportDocument document, double[] widths, IReadOnlyList<IReadOnlyList<ReportDocumentCell>> rows, int index, int count)
        {
            StringBuilder ops = new();
            double y = PageHeight - Margin - TitleSize;
            Line(ops, document.Title, TitleSize, y, 0, PageWidth - (2 * Margin), Alignment.Start);
            bool sensitive = document.Sensitivity == OutputSensitivity.Sensitive;
            if (sensitive)
            {
                ops.Append("0.70 0 0 rg\n");
                Line(ops, document.ClassificationMarking, TextSize, y, 0, PageWidth - (2 * Margin), Alignment.End);
                ops.Append("0 0 0 rg\n");
            }

            y -= TitleSize * 1.2;
            if (index == 0)
            {
                foreach (ReportDocumentLine line in document.Context)
                {
                    y -= TextSize * 1.5;
                    Line(ops, $"{line.Label}: {line.Value}", TextSize, y, 0, PageWidth - (2 * Margin), Alignment.Start);
                }

                foreach (string note in document.Notes)
                {
                    y -= TextSize * 1.5;
                    Line(ops, note, TextSize, y, 0, PageWidth - (2 * Margin), Alignment.Start);
                }

                y -= TextSize * 1.5;
            }

            y -= RowHeight;
            Row(ops, [.. document.Columns.Select(c => c.Header)], [.. document.Columns.Select(_ => false)], widths, y, header: true);
            foreach (IReadOnlyList<ReportDocumentCell> row in rows)
            {
                y -= RowHeight;
                Row(ops, [.. row.Select(c => c.Text)], [.. row.Select(c => c.Number is not null)], widths, y, header: false);
            }

            double footer = Margin - FooterSize;
            string page = rightToLeft ? $"صفحة {index + 1} من {count}" : $"Page {index + 1} of {count}";
            string generated = document.GeneratedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
            if (sensitive)
            {
                ops.Append("0.70 0 0 rg\n");
            }

            Line(ops, $"{page} · {generated} · {document.ClassificationMarking}", FooterSize, footer, 0, PageWidth - (2 * Margin), Alignment.Center);
            ops.Append("0 0 0 rg\n");
            return ops.ToString();
        }

        /// <summary>The ToUnicode CMap: each glyph used, the characters it was drawn for (ISO 32000-1 §9.10.3).</summary>
        public string ToUnicode()
        {
            StringBuilder map = new("/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def /CMapName /Adobe-Identity-UCS def /CMapType 2 def 1 begincodespacerange <0000> <FFFF> endcodespacerange\n");
            foreach (KeyValuePair<ushort, string>[] chunk in _glyphText.OrderBy(g => g.Key).Chunk(100))
            {
                map.Append(CultureInfo.InvariantCulture, $"{chunk.Length} beginbfchar\n");
                foreach ((ushort glyph, string text) in chunk)
                {
                    map.Append(CultureInfo.InvariantCulture, $"<{glyph:X4}> <{Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text))}>\n");
                }

                map.Append("endbfchar\n");
            }

            return map.Append("endcmap CMapName currentdict /CMap defineresource pop end end\n").ToString();
        }

        /// <summary>A table row: cells laid out from the start side, text truncated to its column, numbers aligned to the end in a left-to-right output.</summary>
        private void Row(StringBuilder ops, IReadOnlyList<string> cells, IReadOnlyList<bool> numeric, double[] widths, double y, bool header)
        {
            double tableWidth = widths.Sum();
            if (header)
            {
                ops.Append(CultureInfo.InvariantCulture, $"0.90 0.90 0.90 rg {X(0, tableWidth):0.##} {y - (RowHeight * 0.3):0.##} {tableWidth:0.##} {RowHeight:0.##} re f 0 0 0 rg\n");
            }

            ops.Append(CultureInfo.InvariantCulture, $"0.75 0.75 0.75 RG 0.4 w {Margin:0.##} {y - (RowHeight * 0.3):0.##} m {Margin + tableWidth:0.##} {y - (RowHeight * 0.3):0.##} l S 0 0 0 RG\n");
            double offset = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                Line(ops, cells[i], TextSize, y, offset + Padding, widths[i] - (2 * Padding), !rightToLeft && numeric[i] ? Alignment.End : Alignment.Start);
                offset += widths[i];
            }
        }

        /// <summary>
        /// One line of text in the box that starts <paramref name="offset"/> from the page's start side and is <paramref name="width"/> wide:
        /// shaped, put in display order, truncated with an ellipsis to fit, and aligned.
        /// </summary>
        private void Line(StringBuilder ops, string text, double size, double y, double offset, double width, Alignment alignment)
        {
            string fitted = Fit(text, size, width);
            string shaped = ArabicShaping.Shape(fitted);
            string visual = BidiText.Visual(shaped, BidiText.IsRightToLeft(shaped) || (rightToLeft && !shaped.Any(char.IsAsciiLetter)));
            double textWidth = Measure(visual, size);
            double start = alignment switch
            {
                Alignment.Start => rightToLeft ? width - textWidth : 0,
                Alignment.End => rightToLeft ? 0 : width - textWidth,
                Alignment.Center => (width - textWidth) / 2,
                _ => throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Unknown alignment."),
            };
            double x = rightToLeft ? PageWidth - Margin - offset - width + start : Margin + offset + start;
            ops.Append(CultureInfo.InvariantCulture, $"BT /F1 {size:0.##} Tf {x:0.##} {y:0.##} Td <{Glyphs(visual)}> Tj ET\n");
        }

        /// <summary>The text, cut and given an ellipsis when it is wider than the box.</summary>
        private string Fit(string text, double size, double width)
        {
            if (Measure(ArabicShaping.Shape(text), size) <= width)
            {
                return text;
            }

            for (int length = text.Length - 1; length > 0; length--)
            {
                string cut = text[..length].TrimEnd() + "…";
                if (Measure(ArabicShaping.Shape(cut), size) <= width)
                {
                    return cut;
                }
            }

            return "…";
        }

        private double Measure(string text, double size) => text.Sum(c => (double)Face.Advance(Face.GlyphOf(c))) * size / Face.UnitsPerEm;

        /// <summary>The text as glyph ids in hex (Identity-H), each glyph recorded as used with the character it stands for.</summary>
        private string Glyphs(string text)
        {
            StringBuilder hex = new(text.Length * 4);
            foreach (char c in text)
            {
                ushort glyph = Face.GlyphOf(c);
                UsedGlyphs.Add(glyph);
                if (glyph != 0)
                {
                    _glyphText.TryAdd(glyph, c.ToString());
                }

                hex.Append(CultureInfo.InvariantCulture, $"{glyph:X4}");
            }

            return hex.ToString();
        }

        private double X(double offset, double width) => rightToLeft ? PageWidth - Margin - offset - width : Margin + offset;
    }

    private enum Alignment
    {
        Start = 1,
        End = 2,
        Center = 3,
    }
}

/// <summary>The fonts the PDF renderer embeds, from this assembly's resources (licence alongside: <c>Fonts/LICENSE-DejaVu.txt</c>).</summary>
internal static class FontResource
{
    public const string DejaVuSansName = "PMPlatform.Infrastructure.Reports.Rendering.Fonts.DejaVuSans.ttf";

    public static byte[] DejaVuSans()
    {
        using Stream stream = typeof(FontResource).Assembly.GetManifestResourceStream(DejaVuSansName)
                              ?? throw new InvalidOperationException($"The embedded font {DejaVuSansName} is missing.");
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }
}
