using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// WF-03's typed adapter to WF-08 (ADR-003 §8.2 edge 11): every rebaseline after the first APPROVED baseline implements an approved
/// change (BR-SCH-034, DCL-SCH-21). The claim is a REBASELINE of the project's ACTIVE Approved Baseline at its version; WF-08 refuses an
/// authorisation of another project, another kind of change, or a baseline that has moved since the change was approved.
/// </summary>
internal sealed class RebaselineAuthorization(IChangeAuthorizations authorizations)
{
    /// <summary>Whether the authorisation would apply now: checked when a candidate goes to WF-11 and takes effect only on its approval.</summary>
    public async Task<bool> IsApplicableAsync(ProjectFacts project, ProjectBaseline active, Guid changeAuthorizationId, CancellationToken cancellationToken) =>
        await authorizations.CheckAsync(ClaimOf(project, active, changeAuthorizationId), cancellationToken).ConfigureAwait(false) == ChangeAuthorizationVerdict.Applicable;

    /// <summary>
    /// Applies the candidate's authorisation as it replaces <paramref name="active"/>, in the activation's unit of work, once: the same
    /// candidate applying it again is the same application. False when WF-08 refuses it, and then nothing may activate.
    /// </summary>
    public async Task<bool> ApplyAsync(
        ProjectFacts project, ProjectBaseline active, ProjectBaseline candidate, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.ChangeAuthorizationId is not { } authorizationId)
        {
            return false;
        }

        ChangeAuthorizationUse use = new(actorId, ChangeAuthorizationUse.ReferenceOf(ChangeTargets.ScheduleModule, ChangeTargets.ProjectBaseline, candidate.Id), now);
        return await authorizations.ApplyAsync(ClaimOf(project, active, authorizationId), use, cancellationToken).ConfigureAwait(false)
            is ChangeAuthorizationVerdict.Applied or ChangeAuthorizationVerdict.Replayed;
    }

    private static ChangeAuthorizationClaim ClaimOf(ProjectFacts project, ProjectBaseline active, Guid changeAuthorizationId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(active);
        return new ChangeAuthorizationClaim(changeAuthorizationId, project.Id, ChangeAuthorizationScope.Rebaseline, active.Id, active.VersionNo);
    }
}
