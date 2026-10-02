using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>
/// What a person writes on a DRAFT. There is no actual or planned figure: both are derived (ADR-009). A project-level
/// actual is entered only as an override, with its reason.
/// </summary>
public sealed record ProgressSubmissionChanges(NarrativeText? Narrative, ProgressOverride? Override);

/// <summary>ADR-009: an override of the project roll-up, 0–100, with the reason recorded; the calculated value is kept.</summary>
public sealed record ProgressOverride(decimal ActualPercent, NarrativeText Reason);
