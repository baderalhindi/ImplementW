namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>How a visual calculates a field over the projects (or snapshots) of a group (the 18 Nov 2026 data fields), over the cells revealed to the caller only.</summary>
public enum PortfolioFieldAggregation
{
    /// <summary>The projects whose value is revealed; 0, never null, when none is.</summary>
    Count = 1,

    Sum = 2,

    Average = 3,

    /// <summary>The least; for a date, the earliest, as its day number since 1970-01-01.</summary>
    Min = 4,

    /// <summary>The greatest; for a date, the latest, as its day number since 1970-01-01.</summary>
    Max = 5,

    /// <summary>The projects whose yes/no value is yes; 0 when none is.</summary>
    CountTrue = 6,

    /// <summary>Yes ÷ revealed × 100, two places; null when none is revealed.</summary>
    ShareTrue = 7,
}

/// <summary>What a designer field holds: the register's value types.</summary>
public enum PortfolioFieldType
{
    /// <summary>A source-owned state from a closed set: groups and filters a visual, and is counted.</summary>
    Code = 1,

    Count = 2,

    /// <summary>A percentage: counted, averaged, its least or greatest; never summed.</summary>
    Percent = 3,

    Days = 4,

    /// <summary>Money in SAR; sensitive, withheld from a caller not cleared for it.</summary>
    Sar = 5,

    /// <summary>Yes or no (the source writes <c>true</c> or <c>false</c>): groups, filters, is counted and its yes share taken.</summary>
    Boolean = 6,

    /// <summary>A calendar date (<c>yyyy-MM-dd</c>): grouped by year or month, filtered by year, counted, its earliest or latest.</summary>
    Date = 7,

    /// <summary>A moment in UTC (<c>yyyy-MM-ddTHH:mm:ssZ</c>): as a date, by its UTC date.</summary>
    DateTime = 8,

    /// <summary>A record's own text, such as a title (the 20 Nov 2026 record sources): the value is the record's id, the text its label; counted only.</summary>
    Text = 9,

    /// <summary>
    /// A master-data item, a person or a project (the 20 Nov 2026 record sources): the value is its id, its name the label; groups, filters (by id) and is
    /// counted.
    /// </summary>
    Item = 10,
}

/// <summary>What a field's rows are: one per project, or one per published snapshot of a project (its history over time).</summary>
public enum PortfolioFieldGrain
{
    Project = 1,
    Snapshot = 2,
}

/// <summary>
/// One field the designer offers (the 18 Nov 2026 data catalogue): its key <c>&lt;PROJECTION_CODE&gt;.&lt;FIELD_CODE&gt;</c>, the projection it belongs to
/// (code and domain), its field code, its type, whether it is sensitive, the codes a CODE field takes (the register's values, in the source's order), the
/// aggregations its type allows, and its grain.
/// </summary>
public sealed record PortfolioFieldDescriptor(
    string Key, string ProjectionCode, string SourceDomain, string FieldCode, PortfolioFieldType Type, bool IsSensitive,
    IReadOnlyList<string> Values, IReadOnlyList<PortfolioFieldAggregation> Aggregations, PortfolioFieldGrain Grain = PortfolioFieldGrain.Project)
{
    /// <summary>
    /// The aggregations a type allows: a CODE field is only counted (a filter counts one value); a percentage is never summed, since a sum of
    /// percentages means nothing; counts, days and SAR take all five; a yes/no is counted, its yeses counted or their share taken; a date is counted or its
    /// earliest or latest taken.
    /// </summary>
    public static IReadOnlyList<PortfolioFieldAggregation> AggregationsOf(PortfolioFieldType type) => type switch
    {
        PortfolioFieldType.Code or PortfolioFieldType.Text or PortfolioFieldType.Item => [PortfolioFieldAggregation.Count],
        PortfolioFieldType.Percent => [PortfolioFieldAggregation.Count, PortfolioFieldAggregation.Average, PortfolioFieldAggregation.Min, PortfolioFieldAggregation.Max],
        PortfolioFieldType.Count or PortfolioFieldType.Days or PortfolioFieldType.Sar =>
            [PortfolioFieldAggregation.Count, PortfolioFieldAggregation.Sum, PortfolioFieldAggregation.Average, PortfolioFieldAggregation.Min, PortfolioFieldAggregation.Max],
        PortfolioFieldType.Boolean => [PortfolioFieldAggregation.Count, PortfolioFieldAggregation.CountTrue, PortfolioFieldAggregation.ShareTrue],
        PortfolioFieldType.Date or PortfolioFieldType.DateTime => [PortfolioFieldAggregation.Count, PortfolioFieldAggregation.Min, PortfolioFieldAggregation.Max],
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown field type."),
    };

    public bool IsDate() => Type is PortfolioFieldType.Date or PortfolioFieldType.DateTime;
}

/// <summary>
/// A value of one field: the source's exact string, or null when it is not revealed to the caller (unknown, restricted or masked). A snapshot-grain cell
/// carries its <see cref="SnapshotIndex"/>, the snapshot's place in the project's history, oldest first.
/// </summary>
public sealed record PortfolioFieldCell(string? Value, int? SnapshotIndex = null);

/// <summary>
/// The governed data catalogue as the designer reads it: the register's fields (FG-01 §4.2, the one the Custom report builder reads), never a table, column or
/// expression (BR-DSH-030). Project declares it and Dashboards implements it over its report rows, since Project may not reference Dashboards (ADR-003 §8.2
/// edge 47 runs Dashboards → Project).
/// </summary>
public interface IPortfolioFieldReader
{
    /// <summary>The register's project-grain fields, then its snapshot-grain fields, each in the register's order.</summary>
    public IReadOnlyList<PortfolioFieldDescriptor> Fields { get; }

    /// <summary>
    /// For each project id given (and reached by the caller), its cells: a project-grain key's one cell, and a snapshot-grain key's one cell per published
    /// snapshot (<see cref="PortfolioFieldCell.SnapshotIndex"/> set), each authorised and masked exactly as a report cell (sensitive fields not masked
    /// wholesale, ineligible projects left out; FG-02 §8.2). A project the reader does not return, and a key not in <see cref="Fields"/>, has no cell.
    /// </summary>
    public Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>>> ReadAsync(
        Guid callerId, IReadOnlyCollection<Guid> projectIds, IReadOnlyCollection<string> fieldKeys, DateTimeOffset now, CancellationToken cancellationToken);
}
