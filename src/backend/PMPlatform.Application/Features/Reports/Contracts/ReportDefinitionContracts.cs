using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports.Contracts;

/// <summary>
/// A DRAFT version's content, as a whole (ADM-037; R-5): labels, audience family, the projection that decides its rows, whether saved views are
/// allowed, the roles that may run it, its parameters and its columns. Nothing in it is an expression: it names registered projections and
/// fields by code (BR-RPT-046).
/// </summary>
public sealed record ReportDefinitionContent(
    BilingualLabel Name,
    BilingualLabel? Description,
    ReportAudienceFamily AudienceFamily,
    string PrimaryProjectionCode,
    bool AllowsSavedViews,
    IReadOnlyList<string> AudienceRoleCodes,
    IReadOnlyList<ReportParameterDefinitionInput> Parameters,
    IReadOnlyList<ReportColumnInput> Columns);

public sealed record ReportColumnInput(string SourceEntityCode, string FieldCode, BilingualLabel Label, bool IsDefaultVisible, Guid? DataClassificationItemId);

public sealed record ReportParameterDefinitionInput(
    string Code, BilingualLabel Label, ReportParameterDataType DataType, bool IsRequired, string? SourceEntityCode, string? FieldCode,
    IReadOnlyList<ReportParameterOptionInput> Options);

public sealed record ReportParameterOptionInput(string ValueCode, BilingualLabel Label, string? CatalogueEntryReference);

/// <summary>One version of a governed report, whole (ADM-037).</summary>
public sealed record ReportDefinitionDetail(
    Guid Id,
    ReportCode Code,
    int VersionNo,
    BilingualLabel Name,
    BilingualLabel? Description,
    ReportAudienceFamily AudienceFamily,
    string PrimaryProjectionCode,
    bool AllowsSavedViews,
    GovernedLifecycleState LifecycleState,
    Guid? ValidatedByUserId,
    DateTimeOffset? ValidatedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? RetiredAt,
    IReadOnlyList<string> AudienceRoleCodes,
    IReadOnlyList<ReportParameterDefinitionInput> Parameters,
    IReadOnlyList<ReportColumnInput> Columns,
    IReadOnlyList<string> CatalogueEntries,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record ReportDefinitionSummary(
    Guid Id, ReportCode Code, int VersionNo, BilingualLabel Name, GovernedLifecycleState LifecycleState, DateTimeOffset? PublishedAt, DateTimeOffset? RetiredAt, DateTimeOffset UpdatedAt);

public sealed record ReportDefinitionPage(IReadOnlyList<ReportDefinitionSummary> Items, int Page, int PageSize, int TotalCount);

public sealed record ReportDefinitionQuery(ReportCode? Code, GovernedLifecycleState? LifecycleState);
