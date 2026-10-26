using PMPlatform.Application.Common.Projections;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports.Contracts;

/// <summary>
/// A field the SCR-138 explorer offers (MOD-061): an entry of the REPORT_RULES version in force, as the register describes the field it names —
/// its type, the operators it takes when filterable, its closed set of values, and its projection's semantic state. Nothing else is offered
/// and nothing else is accepted (BR-RPT-045).
/// </summary>
public sealed record ReportAllowlistEntryView(
    Guid Id,
    string SourceEntityCode,
    string FieldCode,
    BilingualLabel Label,
    ReportValueType ValueType,
    bool IsFilterable,
    bool IsSortable,
    IReadOnlyList<ReportFilterOperator> Operators,
    IReadOnlyList<string> Values,
    string? ProjectionCode,
    ProjectionSemanticState? SemanticState,
    bool IsSensitive);

/// <summary>The allowlist in force: the REPORT_RULES version it comes from, and its entries.</summary>
public sealed record ReportAllowlistEntryPage(
    IReadOnlyList<ReportAllowlistEntryView> Items, int Page, int PageSize, int TotalCount, Guid ConfigurationVersionId, int ConfigurationVersionNo);
