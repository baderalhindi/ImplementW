using System.Globalization;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <inheritdoc cref="IStagePlanChangeAuthorization"/>
internal sealed class StagePlanChangeAuthorization(IChangeAuthorizations authorizations) : IStagePlanChangeAuthorization
{
    public async Task<bool> ApplyAsync(
        Guid changeAuthorizationId, Guid projectId, int revisionNo, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ChangeAuthorizationClaim claim = new(changeAuthorizationId, projectId, ChangeAuthorizationScope.StagePlanChange, projectId, revisionNo);

        // The plan revision this application produces names it, so a later change of the plan is another use.
        string reference = ChangeAuthorizationUse.ReferenceOf(ChangeTargets.ProjectModule, ChangeTargets.ProjectStagePlan, projectId)
                           + ":r" + (revisionNo + 1).ToString(CultureInfo.InvariantCulture);
        return await authorizations.ApplyAsync(claim, new ChangeAuthorizationUse(actorId, reference, now), cancellationToken).ConfigureAwait(false)
            == ChangeAuthorizationVerdict.Applied;
    }
}
