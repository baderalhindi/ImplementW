using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.ChangeRequest.Contracts;

/// <summary>
/// A new change request of the project, DRAFT. A draft may be incomplete; what its change type needs is checked on submission.
/// No materiality is ever part of a draft: the server computes it.
/// </summary>
public sealed record ChangeRequestDraft(Guid ProjectId, ChangeType ChangeType, ChangeRequestChanges Fields);

/// <summary>A request's own fields, as a whole (R-5). Its type, project and status change only through the commands.</summary>
public sealed record ChangeRequestChanges(
    NarrativeText Title,
    NarrativeText Justification,
    Money? CostImpactSar,
    int? ScheduleImpactDays,
    NarrativeText? ScopeImpact,
    bool IsContractualObligation,
    Guid? RequestedGovernanceProfileItemId);

/// <summary>A project's change requests, filtered by any of <see cref="Statuses"/> and any of <see cref="ChangeTypes"/>.</summary>
public sealed record ChangeRequestQuery(Guid ProjectId, IReadOnlyCollection<ChangeRequestStatus> Statuses, IReadOnlyCollection<ChangeType> ChangeTypes);

/// <summary>
/// A project's change authorisations, or one request's, filtered by any of <see cref="Scopes"/> and any of <see cref="Statuses"/>:
/// what a target module's screen offers when a change is applied there.
/// </summary>
public sealed record ChangeAuthorizationQuery(
    Guid ProjectId, Guid? ChangeRequestId, IReadOnlyCollection<ChangeAuthorizationScope> Scopes, IReadOnlyCollection<ChangeAuthorizationStatus> Statuses);
