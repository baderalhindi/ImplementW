using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Domain.ChangeRequest;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>
/// When an authorisation applies (WF-08 VAL-CHG-015, VAL-CHG-016, BR-CHG-021): to its own project and kind of change only; to the very
/// target version it pins, so a target that moved since approval is refused; while its request is in IMPLEMENTATION; and once. In
/// that order, so the answer names the first thing wrong. A check passes no reference; an application passes its use's, so the same
/// application again is recognised.
/// </summary>
internal static class ChangeAuthorizationRules
{
    public static ChangeAuthorizationVerdict Judge(
        ChangeAuthorization? authorization, ChangeRequestEntity? request, ChangeAuthorizationClaim claim, string? appliedReference, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return authorization is null || request is null ? ChangeAuthorizationVerdict.NotFound
            : request.ProjectId != claim.ProjectId || authorization.AuthorizationScope != claim.Scope ? ChangeAuthorizationVerdict.OutOfScope
            : authorization.Status == ChangeAuthorizationStatus.Applied
                ? appliedReference is not null && authorization.AppliedReference == appliedReference ? ChangeAuthorizationVerdict.Replayed : ChangeAuthorizationVerdict.Consumed
            : authorization.Status != ChangeAuthorizationStatus.Issued || authorization.ExpiresAt <= now ? ChangeAuthorizationVerdict.Ended
            : authorization.TargetId != claim.TargetId || authorization.TargetRevisionNo != claim.TargetRevisionNo ? ChangeAuthorizationVerdict.TargetMoved
            : request.Status == ChangeRequestStatus.Implementation ? ChangeAuthorizationVerdict.Applicable
            : ChangeAuthorizationVerdict.NotImplementing;
    }
}
