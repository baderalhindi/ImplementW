using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// A field a report column, a filter or an allowlist entry may name: one of the row project's own identity fields (<c>PROJECT</c>), or a field of
/// a registered projection under the projection's entity code (FG-01's register, edge 30). Its type decides the operators it takes (FG-02 §6.1)
/// and whether it can be ordered.
/// </summary>
internal sealed record ReportField(string SourceEntityCode, string FieldCode, ReportValueType Type, bool IsSensitive, IReadOnlyList<string> Values, ProjectionDescriptor? Projection)
{
    private static readonly ReportFilterOperator[] Equality = [ReportFilterOperator.Eq, ReportFilterOperator.Neq, ReportFilterOperator.In];

    private static readonly ReportFilterOperator[] Ordered =
    [
        ReportFilterOperator.Eq, ReportFilterOperator.Neq, ReportFilterOperator.In, ReportFilterOperator.Gt, ReportFilterOperator.Gte,
        ReportFilterOperator.Lt, ReportFilterOperator.Lte, ReportFilterOperator.Between,
    ];

    public string Key => $"{SourceEntityCode}.{FieldCode}";

    /// <summary>The row's own identity: shown on every row the caller reaches, read from WF-01 with the row.</summary>
    public bool IsIdentity => Projection is null;

    /// <summary>The operators the field takes (BR-RPT-046: a closed set, by type).</summary>
    public IReadOnlyList<ReportFilterOperator> Operators => Type switch
    {
        ReportValueType.Text => [.. Equality, ReportFilterOperator.Contains],
        ReportValueType.Code or ReportValueType.Reference => Equality,
        ReportValueType.Boolean => [ReportFilterOperator.Eq],
        ReportValueType.Count or ReportValueType.Percent or ReportValueType.Days or ReportValueType.Sar
            or ReportValueType.Date or ReportValueType.DateTime => Ordered,
        _ => throw new InvalidOperationException($"{Key} has an unknown type."),
    };

    /// <summary>A department or an entity is a reference, not an order: it is filtered by, never sorted by.</summary>
    public bool IsSortableType => Type != ReportValueType.Reference;

    /// <summary>Whether the field may stand beside the row's own grain: identity always; a projection's field only of the same grain.</summary>
    public bool FitsGrain(ProjectionDescriptor basis) =>
        IsIdentity || (basis.Grain == ProjectionGrain.Snapshot ? Projection!.Code == basis.Code : Projection!.Grain == ProjectionGrain.Project);
}

/// <summary>The fields reports may name, read from FG-01's register once per scope.</summary>
internal sealed class ReportFields
{
    /// <summary>The row project's own identity, as configuration names it.</summary>
    public const string ProjectEntity = "PROJECT";

    public const string FormalProjectId = "FORMAL_PROJECT_ID";
    public const string Title = "TITLE";
    public const string Department = "DEPARTMENT";
    public const string ExternalEntity = "EXTERNAL_ENTITY";

    /// <summary>The explorer's rows: the projects the caller may see under WF-01's own view permission (SCR-138 over the project register).</summary>
    public const string ExplorerBasis = "PROJECT.LIFECYCLE_STATE";

    private static readonly ReportField[] Identity =
    [
        new(ProjectEntity, FormalProjectId, ReportValueType.Text, false, [], null),
        new(ProjectEntity, Title, ReportValueType.Text, false, [], null),
        new(ProjectEntity, Department, ReportValueType.Reference, false, [], null),
        new(ProjectEntity, ExternalEntity, ReportValueType.Reference, false, [], null),
    ];

    private readonly Dictionary<(string Entity, string Field), ReportField> _fields;
    private readonly Dictionary<string, ProjectionDescriptor> _projections;

    public ReportFields(IProjectionRowReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _projections = reader.Projections.ToDictionary(p => p.Code, StringComparer.Ordinal);
        _fields = Identity.Concat(reader.Projections.SelectMany(p => p.Fields.Select(f => new ReportField(p.EntityCode, f.Code, TypeOf(f.Type), f.IsSensitive, f.Values, p))))
            .ToDictionary(f => (f.SourceEntityCode, f.FieldCode));
        EntityCodes = new HashSet<string>(_fields.Keys.Select(k => k.Entity), StringComparer.Ordinal);
    }

    /// <summary>Every source entity a field may name: <c>PROJECT</c> and each reportable projection's entity code.</summary>
    public IReadOnlySet<string> EntityCodes { get; }

    public ReportField? Find(string sourceEntityCode, string fieldCode) => _fields.GetValueOrDefault((sourceEntityCode, fieldCode));

    public ProjectionDescriptor? Projection(string code) => _projections.GetValueOrDefault(code);

    private static ReportValueType TypeOf(ProjectionValueType type) => type switch
    {
        ProjectionValueType.Code => ReportValueType.Code,
        ProjectionValueType.Count => ReportValueType.Count,
        ProjectionValueType.Percent => ReportValueType.Percent,
        ProjectionValueType.Days => ReportValueType.Days,
        ProjectionValueType.Sar => ReportValueType.Sar,
        ProjectionValueType.Boolean => ReportValueType.Boolean,
        ProjectionValueType.Date => ReportValueType.Date,
        ProjectionValueType.DateTime => ReportValueType.DateTime,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown projection value type."),
    };
}
