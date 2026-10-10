using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using PMPlatform.Application.Features.Reports;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Reports.Rendering;

/// <summary>
/// XLSX (FG-02 §9.1; ADR-005), written as Office Open XML directly: a "Report" sheet — a bold, frozen header row, then typed cells: a number the
/// source typed is a number with its format (SAR with two places), every other value an inline string — and a "Context" sheet with the parameters,
/// Generated At, each source's semantic state, freshness, as-of and coverage, and the classification. No cell is ever written as a formula: text is
/// an inline string, and text a spreadsheet could read as one is also marked quote-prefixed (FG-02 §9.2). An Arabic workbook reads right to left.
/// </summary>
internal sealed class XlsxReportRenderer : IReportRenderer
{
    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    // Cell styles (cellXfs order in Styles): default, header, quote-prefixed text, SAR, percent, integer, no value.
    private const int StyleHeader = 1;
    private const int StyleQuoted = 2;
    private const int StyleSar = 3;
    private const int StylePercent = 4;
    private const int StyleInteger = 5;
    private const int StyleUnknown = 6;

    public ReportExportFormat Format => ReportExportFormat.Xlsx;

    public RenderedReport Render(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        bool rtl = document.Language == Language.Ar;
        int neutralized = 0;
        bool anyStale = document.Rows.Any(r => r.Any(c => c.IsStale));

        List<List<Cell>> report = [[.. document.Columns.Select(c => Text(c.Header, StyleHeader, ref neutralized))]];
        if (anyStale)
        {
            report[0].Add(Text(rtl ? "ملاحظات البيانات" : "Data notes", StyleHeader, ref neutralized));
        }

        foreach (IReadOnlyList<ReportDocumentCell> row in document.Rows)
        {
            List<Cell> cells = [.. row.Select((c, i) => Value(c, document.Columns[i].Type, ref neutralized))];
            if (anyStale)
            {
                string stale = string.Join("; ", row.Select((c, i) => (c, i)).Where(x => x.c.IsStale).Select(x => document.Columns[x.i].Header));
                cells.Add(Text(stale, 0, ref neutralized));
            }

            report.Add(cells);
        }

        List<List<Cell>> context =
        [
            [Text(document.Title, StyleHeader, ref neutralized)],
            .. document.Context.Select(l => new List<Cell> { Text(l.Label, StyleHeader, ref neutralized), Text(l.Value, 0, ref neutralized) }),
            .. document.Notes.Select(n => new List<Cell> { Text(n, 0, ref neutralized) }),
        ];

        using MemoryStream buffer = new();
        using (ZipArchive zip = new(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Part(zip, "[Content_Types].xml", ContentTypes());
            Part(zip, "_rels/.rels", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="{Relationships}/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Part(zip, "xl/workbook.xml", Workbook(rtl));
            Part(zip, "xl/_rels/workbook.xml.rels", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="{Relationships}/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="{Relationships}/worksheet" Target="worksheets/sheet2.xml"/><Relationship Id="rId3" Type="{Relationships}/styles" Target="styles.xml"/></Relationships>""");
            Part(zip, "xl/styles.xml", Styles());
            Part(zip, "xl/worksheets/sheet1.xml", Sheet(report, rtl, freezeHeader: true));
            Part(zip, "xl/worksheets/sheet2.xml", Sheet(context, rtl, freezeHeader: false));
        }

        return new RenderedReport(buffer.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx", neutralized);
    }

    /// <summary>A typed cell: a number as a number with its format; a value with no number as words; no value as its reason, styled apart.</summary>
    private static Cell Value(ReportDocumentCell cell, ReportValueType type, ref int neutralized)
    {
        if (cell.Number is { } number)
        {
            int style = type switch
            {
                ReportValueType.Sar => StyleSar,
                ReportValueType.Percent => StylePercent,
                ReportValueType.Count or ReportValueType.Days => StyleInteger,
                ReportValueType.Code or ReportValueType.Text or ReportValueType.Reference or ReportValueType.Boolean or ReportValueType.Date or ReportValueType.DateTime => 0,
                _ => 0,
            };
            return new Cell(number, IsNumber: true, style);
        }

        Cell text = Text(cell.Text, 0, ref neutralized);
        return cell.IsUnknown ? text with { Style = StyleUnknown } : text;
    }

    private static Cell Text(string text, int style, ref int neutralized)
    {
        bool formulaLike = SpreadsheetSafety.IsFormulaLike(text);
        neutralized += formulaLike ? 1 : 0;
        return new Cell(text, IsNumber: false, formulaLike ? StyleQuoted : style);
    }

    private static string Sheet(List<List<Cell>> rows, bool rtl, bool freezeHeader)
    {
        StringBuilder xml = new($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="{Main}"><sheetViews><sheetView workbookViewId="0"{(rtl ? " rightToLeft=\"1\"" : string.Empty)}>""");
        if (freezeHeader && rows.Count > 1)
        {
            xml.Append("""<pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/>""");
        }

        xml.Append("</sheetView></sheetViews>");
        int columns = rows.Max(r => r.Count);
        xml.Append("<cols>");
        for (int c = 0; c < columns; c++)
        {
            int width = Math.Clamp(rows.Where(r => c < r.Count).Max(r => r[c].Value.Length) + 2, 8, 60);
            xml.Append(CultureInfo.InvariantCulture, $"""<col min="{c + 1}" max="{c + 1}" width="{width}" customWidth="1"/>""");
        }

        xml.Append("</cols><sheetData>");
        for (int r = 0; r < rows.Count; r++)
        {
            xml.Append(CultureInfo.InvariantCulture, $"""<row r="{r + 1}">""");
            for (int c = 0; c < rows[r].Count; c++)
            {
                Cell cell = rows[r][c];
                string reference = $"{ColumnName(c)}{(r + 1).ToString(CultureInfo.InvariantCulture)}";
                string style = cell.Style == 0 ? string.Empty : $" s=\"{cell.Style.ToString(CultureInfo.InvariantCulture)}\"";
                xml.Append(cell.IsNumber
                    ? $"""<c r="{reference}"{style}><v>{cell.Value}</v></c>"""
                    : $"""<c r="{reference}"{style} t="inlineStr"><is><t xml:space="preserve">{Escape(cell.Value)}</t></is></c>""");
            }

            xml.Append("</row>");
        }

        return xml.Append("</sheetData></worksheet>").ToString();
    }

    private static string Workbook(bool rtl)
    {
        string report = rtl ? "التقرير" : "Report";
        string context = rtl ? "السياق" : "Context";
        return $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="{Main}" xmlns:r="{Relationships}"><bookViews><workbookView/></bookViews><sheets><sheet name="{report}" sheetId="1" r:id="rId1"/><sheet name="{context}" sheetId="2" r:id="rId2"/></sheets></workbook>""";
    }

    private static string Styles() =>
        $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="{Main}"><numFmts count="2"><numFmt numFmtId="164" formatCode="#,##0.00"/><numFmt numFmtId="165" formatCode="0.##&quot;%&quot;"/></numFmts><fonts count="3"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font><font><i/><sz val="11"/><color rgb="FF6B6B6B"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="7"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="49" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" quotePrefix="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="1" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/></cellXfs></styleSheet>""";

    private static string ContentTypes() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""";

    private static void Part(ZipArchive zip, string name, string xml)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xml);
        stream.Write(bytes);
    }

    /// <summary>XML text: escaped, and without the characters XML 1.0 cannot hold.</summary>
    private static string Escape(string text) =>
        SecurityElement.Escape(new string([.. text.Where(c => c is '\t' or '\n' or '\r' || (!char.IsControl(c) && c != '￾' && c != '￿'))])) ?? string.Empty;

    private static string ColumnName(int index)
    {
        string name = string.Empty;
        for (int n = index + 1; n > 0; n = (n - 1) / 26)
        {
            name = (char)('A' + ((n - 1) % 26)) + name;
        }

        return name;
    }

    private sealed record Cell(string Value, bool IsNumber, int Style);
}
