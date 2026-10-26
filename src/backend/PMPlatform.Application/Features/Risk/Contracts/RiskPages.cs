namespace PMPlatform.Application.Features.Risk.Contracts;

/// <summary>A project's risks, most recently changed first (R-29; indexing-strategy P-2).</summary>
public sealed record RiskPage(IReadOnlyList<RiskDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A risk's assessment versions, newest first.</summary>
public sealed record RiskAssessmentPage(IReadOnlyList<RiskAssessmentDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A risk's acceptances, newest first.</summary>
public sealed record RiskAcceptancePage(IReadOnlyList<RiskAcceptanceDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A risk's treatment actions, oldest first.</summary>
public sealed record RiskTreatmentActionPage(IReadOnlyList<RiskTreatmentActionDetail> Items, int Page, int PageSize, int TotalCount);
