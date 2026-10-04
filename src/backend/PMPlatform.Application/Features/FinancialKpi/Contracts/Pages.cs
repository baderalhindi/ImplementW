namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>A project's financial fields and their source modes, in field order (R-29).</summary>
public sealed record FinancialSourceModePage(IReadOnlyList<FinancialSourceModeDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's commitment versions, newest first (R-29).</summary>
public sealed record FinancialCommitmentPage(IReadOnlyList<FinancialCommitmentDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's financial update revisions, newest first (R-29).</summary>
public sealed record FinancialProgressUpdatePage(IReadOnlyList<FinancialProgressUpdateDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's published financial snapshots, latest first (R-29).</summary>
public sealed record PublishedFinancialSnapshotPage(IReadOnlyList<PublishedFinancialSnapshotDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's live financial position: one item for a project the caller may see, none otherwise (R-29).</summary>
public sealed record FinancialPositionPage(IReadOnlyList<FinancialPositionDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's KPI assignments, newest first (R-29).</summary>
public sealed record KpiAssignmentPage(IReadOnlyList<KpiAssignmentDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>An assignment's target versions, newest first (R-29).</summary>
public sealed record KpiTargetVersionPage(IReadOnlyList<KpiTargetVersionDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>An assignment's measurements, latest period first (R-29).</summary>
public sealed record KpiMeasurementPage(IReadOnlyList<KpiMeasurementDetail> Items, int Page, int PageSize, int TotalCount);
