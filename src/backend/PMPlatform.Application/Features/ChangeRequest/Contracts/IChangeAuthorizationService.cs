using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.ChangeRequest.Contracts;

/// <summary>
/// The change authorisations approval issued (TASK-060), as people read them: a project's, or one request's, and one by id. Issuing
/// is the approval's and applying is the target module's (<see cref="IChangeAuthorizations"/>); neither is an operation here.
/// </summary>
public interface IChangeAuthorizationService
{
    /// <summary>Empty for a project the caller may not see.</summary>
    public Task<ChangeAuthorizationPage> ListAsync(Guid callerId, ChangeAuthorizationQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<ChangeAuthorizationDetail>> GetAsync(Guid callerId, Guid changeAuthorizationId, CancellationToken cancellationToken);
}
