using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// WF-14's typed adapter to WF-08 (ADR-003 §8.2 edge 12): any change to an ACTIVE Approved Budget implements an approved change (ERD
/// <c>financial_commitment.change_authorization_id</c>). The claim is a COMMITMENT_CHANGE of the project's ACTIVE APPROVED_BUDGET version
/// at its version number; WF-08 refuses an authorisation of another project, another kind of change, or a budget that has moved since.
/// </summary>
internal sealed class CommitmentChangeAuthorization(IChangeAuthorizations authorizations)
{
    /// <summary>Whether the authorisation would apply now: checked when the version goes to WF-11, whose approval makes it take effect.</summary>
    public async Task<bool> IsApplicableAsync(ProjectFacts project, FinancialCommitment active, Guid changeAuthorizationId, CancellationToken cancellationToken) =>
        await authorizations.CheckAsync(ClaimOf(project, active, changeAuthorizationId), cancellationToken).ConfigureAwait(false) == ChangeAuthorizationVerdict.Applicable;

    /// <summary>
    /// Applies the version's authorisation as it supersedes <paramref name="active"/>, in the activation's unit of work, once. False when
    /// WF-08 refuses it, and then the version may not activate.
    /// </summary>
    public async Task<bool> ApplyAsync(
        ProjectFacts project, FinancialCommitment active, FinancialCommitment version, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.ChangeAuthorizationId is not { } authorizationId)
        {
            return false;
        }

        ChangeAuthorizationUse use = new(actorId, ChangeAuthorizationUse.ReferenceOf(ChangeTargets.FinancialKpiModule, ChangeTargets.FinancialCommitment, version.Id), now);
        return await authorizations.ApplyAsync(ClaimOf(project, active, authorizationId), use, cancellationToken).ConfigureAwait(false)
            is ChangeAuthorizationVerdict.Applied or ChangeAuthorizationVerdict.Replayed;
    }

    private static ChangeAuthorizationClaim ClaimOf(ProjectFacts project, FinancialCommitment active, Guid changeAuthorizationId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(active);
        return new ChangeAuthorizationClaim(changeAuthorizationId, project.Id, ChangeAuthorizationScope.CommitmentChange, active.Id, active.VersionNo);
    }
}
