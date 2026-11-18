namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>How a visual calculates a field over the projects of a group (the 18 Nov 2026 data fields), over the cells revealed to the caller only.</summary>
public enum PortfolioFieldAggregation
{
    /// <summary>The projects whose value is revealed; 0, never null, when none is.</summary>
    Count = 1,

    Sum = 2,

    Average = 3,

    Min = 4,

    Max = 5,
}

/// <summary>What a designer field holds: the register's value types a visual can calculate with (<c>BOOLEAN</c>, <c>DATE</c> and <c>DATE_TIME</c> are not offered).</summary>
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
}

/// <summary>
/// One field the designer offers (the 18 Nov 2026 data catalogue): its key <c>&lt;PROJECTION_CODE&gt;.&lt;FIELD_CODE&gt;</c>, the projection it belongs to
/// (code and domain), its field code, its type, whether it is sensitive, the codes a CODE field takes (the register's values, in the source's order), and
/// the aggregations its type allows.
/// </summary>
public sealed record PortfolioFieldDescriptor(
    string Key, string ProjectionCode, string SourceDomain, string FieldCode, PortfolioFieldType Type, bool IsSensitive,
    IReadOnlyList<string> Values, IReadOnlyList<PortfolioFieldAggregation> Aggregations)
{
    /// <summary>
    /// The aggregations a type allows: a CODE field is only counted (a filter counts one value); a percentage is never summed, since a sum of
    /// percentages means nothing; counts, days and SAR take all five.
    /// </summary>
    public static IReadOnlyList<PortfolioFieldAggregation> AggregationsOf(PortfolioFieldType type) => type switch
    {
        PortfolioFieldType.Code => [PortfolioFieldAggregation.Count],
        PortfolioFieldType.Percent => [PortfolioFieldAggregation.Count, PortfolioFieldAggregation.Average, PortfolioFieldAggregation.Min, PortfolioFieldAggregation.Max],
        PortfolioFieldType.Count or PortfolioFieldType.Days or PortfolioFieldType.Sar =>
            [PortfolioFieldAggregation.Count, PortfolioFieldAggregation.Sum, PortfolioFieldAggregation.Average, PortfolioFieldAggregation.Min, PortfolioFieldAggregation.Max],
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown field type."),
    };
}

/// <summary>A project's value of one field: the source's exact string, or null when it is not revealed to the caller (unknown, restricted or masked).</summary>
public sealed record PortfolioFieldCell(string? Value);

/// <summary>
/// The governed data catalogue as the designer reads it: the register's project-grain fields (FG-01 §4.2, the one the Custom report builder reads),
/// never a table, column or expression (BR-DSH-030). Project declares it and Dashboards implements it over its report rows, since Project may not
/// reference Dashboards (ADR-003 §8.2 edge 47 runs Dashboards → Project).
/// </summary>
public interface IPortfolioFieldReader
{
    /// <summary>The project-grain fields of the register of types CODE, COUNT, PERCENT, DAYS and SAR, in the register's order.</summary>
    public IReadOnlyList<PortfolioFieldDescriptor> Fields { get; }

    /// <summary>
    /// For each project id given (and reached by the caller), each field key's cell, authorised and masked exactly as a report cell (base projection
    /// PROJECT.LIFECYCLE_STATE, sensitive fields not masked wholesale, ineligible projects left out; FG-02 §8.2). A project the reader does not return,
    /// and a key not in <see cref="Fields"/>, has every cell null.
    /// </summary>
    public Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, PortfolioFieldCell>>> ReadAsync(
        Guid callerId, IReadOnlyCollection<Guid> projectIds, IReadOnlyCollection<string> fieldKeys, DateTimeOffset now, CancellationToken cancellationToken);
}
