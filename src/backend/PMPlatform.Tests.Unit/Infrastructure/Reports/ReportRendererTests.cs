using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using PMPlatform.Application.Features.Reports;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;
using PMPlatform.Infrastructure.Reports.Rendering;
using PMPlatform.Infrastructure.Reports.Rendering.Pdf;

namespace PMPlatform.Tests.Unit.Infrastructure.Reports;

/// <summary>
/// FG-02 §9: the three renderers of ADR-005 write the authorised document they are given, nothing else; a number stays a number, text a spreadsheet
/// could run is never written as a formula (BR-RPT-039), and a missing value is its reason, never empty and never 0.
/// </summary>
public sealed class ReportRendererTests
{
    private static readonly ReportDocumentColumn[] Columns = [new("Project", ReportValueType.Text), new("Budget", ReportValueType.Sar), new("Health", ReportValueType.Code)];

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+SUM(A1)")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    [InlineData("  =cmd")]
    [InlineData("\t=cmd")]
    [InlineData("＝HYPERLINK()")]
    public void TextASpreadsheetCouldRunIsNeutralized(string text)
    {
        Assert.True(SpreadsheetSafety.IsFormulaLike(text));
        Assert.StartsWith("'", SpreadsheetSafety.Neutralize(text, out bool neutralized), StringComparison.Ordinal);
        Assert.True(neutralized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ring road")]
    [InlineData("1-2")]
    [InlineData("مشروع")]
    public void OrdinaryTextIsLeftAsItIs(string text)
    {
        Assert.False(SpreadsheetSafety.IsFormulaLike(text));
        Assert.Equal(text, SpreadsheetSafety.Neutralize(text, out bool neutralized));
        Assert.False(neutralized);
    }

    [Fact]
    public void ACsvIsUtf8WithAByteOrderMarkTypedNumbersAndNeutralizedText()
    {
        RenderedReport csv = new CsvReportRenderer().Render(Document(Language.En,
            [Cell("=HYPERLINK(\"x\")"), Number("1250000.00"), Cell("GREEN")],
            [Cell("Ring road, phase 2"), Unknown("MISSING"), Cell("AMBER", stale: true)]));

        Assert.Equal(("text/csv", "csv", 1), (csv.ContentType, csv.FileExtension, csv.NeutralizedCellCount));
        Assert.Equal(Encoding.UTF8.GetPreamble(), csv.Content[..3]);
        Assert.Equal(
            "Project,Budget,Health,Data notes\r\n" +
            "\"'=HYPERLINK(\"\"x\"\")\",1250000.00,GREEN,\r\n" +
            "\"Ring road, phase 2\",MISSING,AMBER,STALE: Health\r\n",
            Encoding.UTF8.GetString(csv.Content[3..]));
    }

    [Fact]
    public void AnXlsxHoldsNoFormulaAndTypesItsNumbers()
    {
        RenderedReport xlsx = new XlsxReportRenderer().Render(Document(Language.Ar, [Cell("=1+1"), Number("1250000.00"), Unknown("MISSING")]));

        Assert.Equal(1, xlsx.NeutralizedCellCount);
        using ZipArchive zip = new(new MemoryStream(xlsx.Content));
        Assert.Equal(["[Content_Types].xml", "_rels/.rels", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/workbook.xml", "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml"],
            zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        string sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
        Assert.DoesNotContain("<f>", sheet, StringComparison.Ordinal);
        Assert.Contains("rightToLeft=\"1\"", sheet, StringComparison.Ordinal);
        Assert.Contains("s=\"2\" t=\"inlineStr\"><is><t xml:space=\"preserve\">=1+1</t>", sheet, StringComparison.Ordinal);
        Assert.Matches("<c r=\"B2\" s=\"3\"><v>1250000.00</v></c>", sheet);
    }

    [Fact]
    public void APdfEmbedsItsFontAndPaginatesEveryRow()
    {
        List<ReportDocumentCell[]> rows = [.. Enumerable.Range(1, 120).Select(i => new[] { Cell($"مشروع {i}"), Number("1000.00"), Cell("GREEN") })];
        RenderedReport pdf = new PdfReportRenderer().Render(Document(Language.Ar, [.. rows]));

        string text = Encoding.Latin1.GetString(pdf.Content);
        Assert.Equal(("application/pdf", "pdf"), (pdf.ContentType, pdf.FileExtension));
        Assert.StartsWith("%PDF-1.7", text, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", text, StringComparison.Ordinal);
        Assert.Contains("/FontFile2", text, StringComparison.Ordinal);
        Assert.Contains("/ToUnicode", text, StringComparison.Ordinal);
        Assert.Contains("/Identity-H", text, StringComparison.Ordinal);
        int pages = int.Parse(Regex.Match(text, @"/Type /Pages /Kids \[[^\]]*\] /Count (\d+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(pages >= 2, $"{pages} page(s) for 120 rows");
        Assert.Equal(pages, Regex.Count(text, "/Type /Page /Parent"));
    }

    /// <summary>Arabic letters take their joined forms (Unicode Presentation Forms-B), with LAM-ALEF as its ligature; other text is untouched.</summary>
    [Theory]
    [InlineData("بب", "ﺑﺐ")]
    [InlineData("لا", "ﻻ")]
    [InlineData("دار", "ﺩﺍﺭ")]
    [InlineData("Ring road 2", "Ring road 2")]
    public void ArabicIsShapedIntoItsJoinedForms(string text, string shaped) => Assert.Equal(shaped, ArabicShaping.Shape(text));

    /// <summary>A right-to-left line is laid out from the right; a number in it still reads left to right.</summary>
    [Fact]
    public void MixedTextIsLaidOutInDisplayOrder()
    {
        Assert.True(BidiText.IsRightToLeft("مشروع A"));
        Assert.False(BidiText.IsRightToLeft("Project مشروع"));
        Assert.Equal("Project 12", BidiText.Visual("Project 12", rightToLeft: false));
        Assert.Equal("1,250.00 ةميق", BidiText.Visual("قيمة 1,250.00", rightToLeft: true));
        Assert.Equal("(بأ)", BidiText.Visual("(أب)", rightToLeft: true));
    }

    /// <summary>The font is embedded with only the outlines used and the tables a CID font addressed by glyph id needs; ids and metrics unchanged.</summary>
    [Fact]
    public void TheEmbeddedFontIsSubsetToTheGlyphsUsed()
    {
        byte[] whole = FontResource.DejaVuSans();
        TrueTypeFont font = new(whole);
        HashSet<ushort> used = [font.GlyphOf('A'), font.GlyphOf('\uFE91')];
        Assert.All(used, g => Assert.NotEqual(0, g));

        byte[] subset = font.Subset(used);
        int tableCount = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(subset.AsSpan(4));
        string[] tables = [.. Enumerable.Range(0, tableCount).Select(i => Encoding.ASCII.GetString(subset, 12 + (16 * i), 4))];

        Assert.True(subset.Length < whole.Length / 4, $"{subset.Length} of {whole.Length} bytes");
        Assert.Subset(tables.ToHashSet(StringComparer.Ordinal), new HashSet<string>(["glyf", "head", "hhea", "hmtx", "loca", "maxp"], StringComparer.Ordinal));
        Assert.DoesNotContain("cmap", tables);
        Assert.Equal(tables.Order(StringComparer.Ordinal), tables);
    }

    private static ReportDocument Document(Language language, params ReportDocumentCell[][] rows) => new(
        "Project Register", language, new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero), OutputSensitivity.Standard, "INTERNAL",
        [new ReportDocumentLine("Generated at", "2026-10-10 12:00 UTC")], Columns, rows, []);

    private static ReportDocumentCell Cell(string text, bool stale = false) => new(text, text, null, stale, IsUnknown: false);

    private static ReportDocumentCell Number(string value) => new(value, value, value, IsStale: false, IsUnknown: false);

    private static ReportDocumentCell Unknown(string reason) => new(reason, reason, null, IsStale: false, IsUnknown: true);
}
