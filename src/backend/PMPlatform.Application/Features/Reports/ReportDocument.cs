using System.Globalization;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// A report laid out for a file (FG-02 §9.1): already authorised, masked, worded and formatted, so a renderer decides layout and encoding only and
/// can neither add nor reveal a value. <see cref="Context"/> carries what FG-02 requires on every output — parameters, Generated At, each source's
/// semantic state, freshness, as-of and coverage, the classification — before the table. <see cref="ClassificationMarking"/> is the marking a
/// paginated output repeats on every page (FG-02 §9.1, TBC-RPT-14).
/// </summary>
public sealed record ReportDocument(
    string Title,
    Language Language,
    DateTimeOffset GeneratedAt,
    OutputSensitivity Sensitivity,
    string ClassificationMarking,
    IReadOnlyList<ReportDocumentLine> Context,
    IReadOnlyList<ReportDocumentColumn> Columns,
    IReadOnlyList<IReadOnlyList<ReportDocumentCell>> Rows,
    IReadOnlyList<string> Notes);

/// <summary>A labelled line of the output's context.</summary>
public sealed record ReportDocumentLine(string Label, string Value);

public sealed record ReportDocumentColumn(string Header, ReportValueType Type);

/// <summary>
/// One cell. <see cref="Text"/> is the value in words for a reader (a PDF, an XLSX text cell); <see cref="Raw"/> is its language-neutral form for a
/// data extract (a CSV): the source's code, an exact decimal, an ISO date, a name, or — for no value — the reason's code, never empty and never 0.
/// <see cref="Number"/> is set only for a numeric value, so a spreadsheet stores it typed (FG-02 §9.1). <see cref="IsUnknown"/> marks a cell with no
/// value, so a renderer can set it apart.
/// </summary>
public sealed record ReportDocumentCell(string Text, string Raw, string? Number, bool IsStale, bool IsUnknown);

/// <summary>A rendered file, and how many cells its formula-injection protection neutralised (FG-02 §9.2; RPT-EVT-035).</summary>
#pragma warning disable CA1819 // The rendered file, written and hashed whole.
public sealed record RenderedReport(byte[] Content, string ContentType, string FileExtension, int NeutralizedCellCount);
#pragma warning restore CA1819

/// <summary>One export format's renderer (FG-02 §21 PDF, XLSX and CSV Renderers): Infrastructure's.</summary>
public interface IReportRenderer
{
    public ReportExportFormat Format { get; }

    public RenderedReport Render(ReportDocument document);
}

/// <summary>Builds a <see cref="ReportDocument"/> from a run: the words, units and markers of every cell in the output's language.</summary>
internal static class ReportDocumentBuilder
{
    public static ReportDocument Build(
        string title, Language language, ReportExecution execution, IReadOnlyList<ReportDocumentLine> parameters, OutputSensitivity sensitivity)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(parameters);
        List<int> shown = [.. execution.Plan.ShownColumns];
        List<IReadOnlyList<ReportDocumentCell>> rows =
            [.. execution.Rows.Select(r => (IReadOnlyList<ReportDocumentCell>)[.. shown.Select(i => Cell(execution.Plan.Columns[i].Field.Type, r.Cells[i], language))])];
        List<ReportDocumentLine> context =
        [
            new(ReportText.In(ReportText.GeneratedAt, language), Timestamp(execution.GeneratedAt)),
            .. parameters,
            .. execution.Sections.Select(s => new ReportDocumentLine(ReportText.In(ReportText.Sources, language), Source(s, language))),
            new(ReportText.In(ReportText.Classification, language), ReportText.Sensitivity(sensitivity == OutputSensitivity.Sensitive, language)),
            new(ReportText.In(ReportText.Rows, language), execution.Rows.Count.ToString(CultureInfo.InvariantCulture)),
        ];
        List<string> notes = [];
        if (rows.Count == 0)
        {
            notes.Add(ReportText.In(ReportText.NoRows, language));
        }

        if (rows.Any(r => r.Any(c => c.IsStale)))
        {
            notes.Add(ReportText.In(ReportText.StaleNote, language));
        }

        return new ReportDocument(
            title, language, execution.GeneratedAt, sensitivity, ReportText.Sensitivity(sensitivity == OutputSensitivity.Sensitive, language), context,
            [.. shown.Select(i => new ReportDocumentColumn(ReportText.In(execution.Plan.Columns[i].Label, language), execution.Plan.Columns[i].Field.Type))],
            rows, notes);
    }

    public static string Timestamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    /// <summary>A value in words: a code named, a figure with its unit, a reference by its name; no value is its reason, never blank or 0.</summary>
    private static ReportDocumentCell Cell(ReportValueType type, ReportCell cell, Language language)
    {
        if (cell.UnknownReason is not null || cell.Value is null)
        {
            ReportUnknownReason why = cell.UnknownReason ?? ReportUnknownReason.Missing;
            return new ReportDocumentCell(ReportText.Reason(why, language), ReportText.ReasonCode(why), null, IsStale: false, IsUnknown: true);
        }

        bool stale = cell.Freshness == Common.Projections.ProjectionFreshness.Stale;
        string value = cell.Value;
        (string text, string raw, string? number) = type switch
        {
            ReportValueType.Code => (ReportText.Code(value, language), value, null),
            ReportValueType.Boolean => (ReportText.Code(value, language), value, null),
            ReportValueType.Reference => (cell.Label is { } label ? ReportText.In(label, language) : value, cell.Label is { } named ? ReportText.In(named, language) : value, null),
            ReportValueType.Text or ReportValueType.Date => (value, value, null),
            ReportValueType.DateTime => (Timestamp(DateTimeOffset.Parse(value, CultureInfo.InvariantCulture)), value, null),
            ReportValueType.Count or ReportValueType.Days => (Grouped(value, "N0") + ReportText.Unit(type, language), value, value),
            ReportValueType.Percent => (value + ReportText.Unit(type, language), value, value),
            ReportValueType.Sar => (Grouped(value, "N2") + ReportText.Unit(type, language), value, value),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown value type."),
        };
        return new ReportDocumentCell(stale ? text + " *" : text, raw, number, stale, IsUnknown: false);
    }

    private static string Source(ReportSection section, Language language)
    {
        string state = ReportText.State(section.Projection.SemanticState, language);
        string fresh = section.UnknownReason is { } reason ? ReportText.Reason(reason, language) : ReportText.Fresh(section.Projection.Freshness, language);
        string asOf = section.Projection.AsOf is { } at ? $", {ReportText.In(ReportText.AsOf, language)} {Timestamp(at)}" : string.Empty;
        string coverage = string.Create(
            CultureInfo.InvariantCulture,
            $"{ReportText.In(ReportText.Coverage, language)} {section.Coverage.IncludedCount}/{section.Coverage.EligibleCount}");
        return $"{section.ProjectionCode} ({section.SourceDomain}): {state}, {fresh}{asOf}, {coverage}";
    }

    private static string Grouped(string value, string format) =>
        decimal.Parse(value, CultureInfo.InvariantCulture).ToString(format, CultureInfo.InvariantCulture);
}
