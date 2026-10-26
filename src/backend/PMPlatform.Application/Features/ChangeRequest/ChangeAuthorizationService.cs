using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>
/// The issued authorisations as people read them (TASK-060): through the project's change requests, so whoever may see those sees them,
/// and a target module's screen can offer the ISSUED ones of its kind. An authorisation the caller may not see does not exist (R-47).
/// </summary>
internal sealed class ChangeAuthorizationService(IChangeRequestRepository repository, ChangeRequestGate gate) : IChangeAuthorizationService
{
    public async Task<ChangeAuthorizationPage> ListAsync(Guid callerId, ChangeAuthorizationQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableProjectAsync(callerId, query.ProjectId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new ChangeAuthorizationPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ChangeAuthorization> items, int total) = await repository.PageAuthorizationsAsync(query, page, cancellationToken).ConfigureAwait(false);
        return new ChangeAuthorizationPage([.. items.Select(ChangeRequestViews.ToDetail)], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<ChangeAuthorizationDetail>> GetAsync(Guid callerId, Guid changeAuthorizationId, CancellationToken cancellationToken) =>
        await repository.FindAuthorizationAsync(changeAuthorizationId, track: false, cancellationToken).ConfigureAwait(false) is { } authorization
        && await gate.ViewableProjectOfAsync(callerId, authorization.ChangeRequestId, cancellationToken).ConfigureAwait(false) is not null
            ? ChangeRequestViews.ToDetail(authorization)
            : AdministrationError.NotFound;
}
