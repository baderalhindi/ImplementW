using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.ChangeRequest.Contracts;

/// <summary>
/// A change request with its latest recorded materiality evaluation and the authorisations its approval issued. Approved and
/// implemented are different states: an APPROVED request has issued authorisations its target modules have not applied.
/// </summary>
public sealed record ChangeRequestDetail(
    Guid Id,
    Guid ProjectId,
    NarrativeText Title,
    NarrativeText Justification,
    ChangeType ChangeType,
    ChangeRequestStatus Status,
    int RevisionNo,
    Guid RequestedByUserId,
    DateTimeOffset? SubmittedAt,
    Money? CostImpactSar,
    int? ScheduleImpactDays,
    NarrativeText? ScopeImpact,
    bool IsContractualObligation,
    Guid? RequestedGovernanceProfileItemId,
    MaterialityAssessment? Materiality,
    IReadOnlyList<ChangeAuthorizationDetail> Authorizations,
    DateTimeOffset? ImplementedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

/// <summary>
/// A materiality classification (ADR-016): the band each dimension triggers — null where the request has no impact on it — and the
/// highest of them, against the cumulative position since the active baseline, under the MATERIALITY_BAND version named.
/// <see cref="EvaluationId"/> is null for a preview: computed by the same rule, recorded nowhere, and advisory until the review records one.
/// </summary>
public sealed record MaterialityAssessment(
    Guid? EvaluationId,
    int RevisionNo,
    DateTimeOffset EvaluatedAt,
    Guid MaterialityConfigurationVersionId,
    Guid? ProjectBaselineId,
    Guid? FinancialCommitmentId,
    Money CumulativeCostImpactSar,
    int CumulativeScheduleImpactDays,
    short? CostBandNo,
    short? ScheduleBandNo,
    short? ScopeBandNo,
    short ResultingBandNo);

/// <summary>
/// An authorisation to change one governed commitment, pinned to the version approved. <see cref="AppliedReference"/> names the
/// target module's record that applied it.
/// </summary>
public sealed record ChangeAuthorizationDetail(
    Guid Id,
    Guid ChangeRequestId,
    Guid ApprovalInstanceId,
    ChangeAuthorizationScope AuthorizationScope,
    string TargetModule,
    string TargetType,
    Guid TargetId,
    int TargetRevisionNo,
    ChangeAuthorizationStatus Status,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? AppliedAt,
    Guid? AppliedByUserId,
    string? AppliedReference);
