using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

internal sealed class FakeAccessRelationshipRepository : FakeStore<AccessRelationship>, IAccessRelationshipRepository
{
    public Dictionary<Guid, ProfileVersionFacts> ProfileVersions { get; } = [];

    public Dictionary<Guid, ProjectFacts> Projects { get; } = [];

    public Task<AccessRelationshipPage> ListAsync(AccessRelationshipQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(PageOf(
            Rows.Values.Where(a => query.UserId is null || a.UserId == query.UserId).Select(Summary),
            query.Page,
            (items, total) => new AccessRelationshipPage(items, query.Page.Page, query.Page.PageSize, total)));

    public Task<AccessRelationshipDetail?> FindDetailAsync(Guid accessRelationshipId, CancellationToken cancellationToken) =>
        Task.FromResult(Rows.TryGetValue(accessRelationshipId, out AccessRelationship? a)
            ? new AccessRelationshipDetail(
                a.Id, a.UserId, RoleOf(a), Guid.Empty, $"{RoleOf(a)}-DEFAULT", a.PermissionProfileVersionId, 1, a.DepartmentId, a.ExternalEntityId, a.ProjectId,
                a.SponsorUserId, a.StartsAt, a.EndsAt, a.EndReason, a.Status, a.CreatedAt, a.CreatedBy, a.UpdatedAt, a.UpdatedBy)
            : null);

    public Task<AccessRelationship?> FindForUpdateAsync(Guid accessRelationshipId, CancellationToken cancellationToken) =>
        Task.FromResult(Track(accessRelationshipId, expectedVersion: null));

    public Task<IReadOnlyList<AccessRelationship>> FindActiveOfUserForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AccessRelationship>>([.. Rows.Values.Where(a => a.UserId == userId && a.Status == AccessRelationshipStatus.Active)]);

    public Task<IReadOnlyList<AccessRelationship>> FindActiveOnProjectForUpdateAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AccessRelationship>>([.. Rows.Values.Where(a => a.ProjectId == projectId && a.Status == AccessRelationshipStatus.Active)]);

    public Task<ProfileVersionFacts?> FindProfileVersionAsync(Guid permissionProfileVersionId, CancellationToken cancellationToken) =>
        Task.FromResult(ProfileVersions.GetValueOrDefault(permissionProfileVersionId));

    public Task<ProjectFacts?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(Projects.GetValueOrDefault(projectId));

    private string RoleOf(AccessRelationship assignment) => ProfileVersions.TryGetValue(assignment.PermissionProfileVersionId, out ProfileVersionFacts? v) ? v.RoleCode : "R00";

    private AccessRelationshipSummary Summary(AccessRelationship a) =>
        new(a.Id, a.UserId, RoleOf(a), a.PermissionProfileVersionId, a.DepartmentId, a.ExternalEntityId, a.ProjectId, a.SponsorUserId, a.StartsAt, a.EndsAt, a.EndReason, a.Status);
}
