using PMPlatform.Domain.Closure;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Closure.Contracts;

/// <summary>A new completion case of the project, DRAFT. A draft may lack its fields; submission checks them.</summary>
public sealed record CompletionCaseDraft(Guid ProjectId, CompletionCaseChanges Fields);

/// <summary>A completion case's own fields, as a whole (R-5). Its project and status change only through the commands.</summary>
public sealed record CompletionCaseChanges(DateOnly? ActualProjectCompletionDate, NarrativeText? CompletionNarrative);

/// <summary>A new closure case of the project, DRAFT: on the normal path if it is COMPLETED, on the terminal path if it is SUSPENDED.</summary>
public sealed record ClosureCaseDraft(Guid ProjectId, ClosureCaseChanges Fields);

/// <summary>A closure case's own fields, as a whole (R-5).</summary>
public sealed record ClosureCaseChanges(NarrativeText? ClosureNarrative);

/// <summary>A project's cases, filtered by any of <see cref="Statuses"/>.</summary>
public sealed record CloseoutCaseQuery(Guid ProjectId, IReadOnlyCollection<CloseoutCaseStatus> Statuses);

/// <summary>An accepted exception to a failed criterion, with why (WF-10 READY_WITH_CONDITIONS; TBC-CLO-005).</summary>
public sealed record ReadinessWaiver(ReadinessCheckCode CheckCode, NarrativeText Reason);

/// <summary>One case's readiness records: exactly one of the two is given.</summary>
public sealed record ReadinessRecordQuery(Guid? CompletionCaseId, Guid? ClosureCaseId);

/// <summary>A new obligation, recorded against a completion case or a terminal closure case (exactly one).</summary>
public sealed record PostProjectObligationDraft(Guid? CompletionCaseId, Guid? ClosureCaseId, PostProjectObligationChanges Fields);

/// <summary>An obligation's own fields, as a whole (R-5). Its status changes only through the commands.</summary>
public sealed record PostProjectObligationChanges(NarrativeText Title, NarrativeText? Description, Guid? OwnerUserId, DateOnly? DueDate);

/// <summary>A project's obligations, filtered by any of <see cref="Statuses"/>.</summary>
public sealed record PostProjectObligationQuery(Guid ProjectId, IReadOnlyCollection<PostProjectObligationStatus> Statuses);
