using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>The <c>identity_access.access_relationship</c> rows as ADM-010 reads and writes them.</summary>
public interface IAccessRelationshipRepository
{
    public Task<AccessRelationshipPage> ListAsync(AccessRelationshipQuery query, CancellationToken cancellationToken);

    public Task<AccessRelationshipDetail?> FindDetailAsync(Guid accessRelationshipId, CancellationToken cancellationToken);

    public Task<AccessRelationship?> FindForUpdateAsync(Guid accessRelationshipId, CancellationToken cancellationToken);

    /// <summary>The user's ACTIVE assignments, tracked.</summary>
    public Task<IReadOnlyList<AccessRelationship>> FindActiveOfUserForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The ACTIVE assignments on the project, tracked.</summary>
    public Task<IReadOnlyList<AccessRelationship>> FindActiveOnProjectForUpdateAsync(Guid projectId, CancellationToken cancellationToken);

    public Task<ProfileVersionFacts?> FindProfileVersionAsync(Guid permissionProfileVersionId, CancellationToken cancellationToken);

    public Task<ProjectFacts?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken);

    public void Add(AccessRelationship accessRelationship);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken);
}
