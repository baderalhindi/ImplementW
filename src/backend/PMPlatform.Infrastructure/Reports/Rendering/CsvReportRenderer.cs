using System.Text;
using PMPlatform.Application.Features.Reports;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Reports.Rendering;

/// <summary>
/// CSV (FG-02 §9.1; ADR-005): a data extract — UTF-8 with a byte-order mark, so a spreadsheet reads Arabic as Arabic, comma-separated, CRLF line
/// ends, RFC 4180 quoting (TBC-RPT-08 leaves the conventions open; these are the common ones). A header row in the output's language, then the rows
/// in language-neutral form: the source's codes, exact decimals, ISO dates, names, and for a value that is not there its reason's code — never empty,
/// never 0. When a value is stale, a last column names the stale columns of its row. Text is neutralised against formula injection (§9.2).
/// </summary>
internal sealed class CsvReportRenderer : IReportRenderer
{
    public ReportExportFormat Format => ReportExportFormat.Csv;

    public RenderedReport Render(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        int neutralized = 0;
        bool anyStale = document.Rows.Any(r => r.Any(c => c.IsStale));
        StringBuilder csv = new();
        List<string> header = [.. document.Columns.Select(c => c.Header)];
        if (anyStale)
        {
            header.Add(document.Language == Language.Ar ? "ملاحظات البيانات" : "Data notes");
        }

        Line(csv, header.Select(h => Field(h, isNumber: false, ref neutralized)));
        foreach (IReadOnlyList<ReportDocumentCell> row in document.Rows)
        {
            List<string> fields = [.. row.Select(c => Field(c.Raw, c.Number is not null, ref neutralized))];
            if (anyStale)
            {
                string stale = string.Join("; ", row.Select((c, i) => (c, i)).Where(x => x.c.IsStale).Select(x => document.Columns[x.i].Header));
                fields.Add(Field(stale.Length == 0 ? string.Empty : $"STALE: {stale}", isNumber: false, ref neutralized));
            }

            Line(csv, fields);
        }

        return new RenderedReport([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())], "text/csv", "csv", neutralized);
    }

    private static void Line(StringBuilder csv, IEnumerable<string> fields) => csv.Append(string.Join(',', fields)).Append("\r\n");

    /// <summary>A number as the source typed it; text neutralised and quoted whenever it holds a delimiter, a quote, a line break or was neutralised.</summary>
    private static string Field(string value, bool isNumber, ref int neutralized)
    {
        if (isNumber)
        {
            return value;
        }

        string safe = SpreadsheetSafety.Neutralize(value, out bool changed);
        neutralized += changed ? 1 : 0;
        return changed || safe.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : safe;
    }
}
