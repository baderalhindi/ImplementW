using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Authorization;

/// <summary>The rows the authorization engine evaluates (TASK-030): IdentityAccess grants and MasterDataConfig classifications.</summary>
internal sealed class AuthorizationRepository(PMPlatformDbContext context, TimeProvider timeProvider) : IAuthorizationRepository
{
    private const string DataClassificationCatalogue = "DATA_CLASSIFICATION";
    private const string FieldClassificationFamily = "FIELD_CLASSIFICATION";

    public async Task<AuthorizationPrincipal?> FindPrincipalAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await context.Set<User>().AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.UserType, u.Status, u.DepartmentId, u.ExternalEntityId })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        ExternalEntityStatus? entityStatus = user.ExternalEntityId is { } entityId
            ? await context.Set<ExternalEntity>().AsNoTracking()
                .Where(e => e.Id == entityId)
                .Select(e => (ExternalEntityStatus?)e.Status)
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            : null;

        // As sign-in reads them (UserAccessRepository): a person acts only while active and, if external, while their
        // entity is; a SERVICE principal never acts through the API.
        bool external = user.UserType == UserType.External;
        bool isActive = user.Status == UserStatus.Active
                        && user.UserType != UserType.Service
                        && (!external || entityStatus == ExternalEntityStatus.Active);

        // ADR-018: the grants of the version each assignment in force is bound to, and only a PUBLISHED one; a draft
        // grants nothing. An external user's assignment counts only for R04 or R08 (ADR-013).
        DateTimeOffset now = timeProvider.GetUtcNow();
        List<EffectiveGrant> grants = await (
                from assignment in context.Set<AccessRelationship>().AsNoTracking()
                join version in context.Set<PermissionProfileVersion>() on assignment.PermissionProfileVersionId equals version.Id
                join profile in context.Set<PermissionProfile>() on version.PermissionProfileId equals profile.Id
                join role in context.Set<Role>() on profile.BaseRoleId equals role.Id
                join grant in context.Set<PermissionProfileGrant>() on version.Id equals grant.PermissionProfileVersionId
                join permission in context.Set<Permission>() on grant.PermissionId equals permission.Id
                where assignment.UserId == userId
                      && assignment.Status == AccessRelationshipStatus.Active
                      && assignment.StartsAt <= now
                      && (assignment.EndsAt == null || assignment.EndsAt > now)
                      && version.LifecycleState == GovernedLifecycleState.Published
                      && (!external || role.IsExternalEligible)
                orderby role.Code, permission.Code, assignment.Id
                select new EffectiveGrant(
                    role.Code, permission.Code, grant.DataScope, assignment.DepartmentId, assignment.ExternalEntityId, assignment.ProjectId,
                    permission.DataClassificationItemId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new AuthorizationPrincipal(user.Id, user.UserType, isActive, user.DepartmentId, user.ExternalEntityId, grants);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetClassificationRanksAsync(CancellationToken cancellationToken) =>
        // Every item, retired ones too: data classified under a retired level stays classified.
        await (
                from item in context.Set<MasterDataItem>().AsNoTracking()
                join catalogue in context.Set<MasterDataCatalogue>() on item.CatalogueId equals catalogue.Id
                where catalogue.Code == DataClassificationCatalogue
                select new { item.Id, item.SortOrder })
            .ToDictionaryAsync(i => i.Id, i => i.SortOrder, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<FieldClassification>> GetFieldClassificationsAsync(string entityCode, CancellationToken cancellationToken)
    {
        // The FIELD_CLASSIFICATION version in force: the latest PUBLISHED one effective now (ERD D-13).
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid? versionId = await (
                from version in context.Set<ConfigurationVersion>().AsNoTracking()
                join family in context.Set<ConfigurationFamily>() on version.ConfigurationFamilyId equals family.Id
                where family.Code == FieldClassificationFamily
                      && version.LifecycleState == GovernedLifecycleState.Published
                      && version.EffectiveFrom <= now
                      && (version.EffectiveTo == null || version.EffectiveTo > now)
                orderby version.EffectiveFrom descending, version.VersionNo descending
                select (Guid?)version.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return versionId is null
            ? []
            : await context.Set<FieldClassificationRule>().AsNoTracking()
                .Where(r => r.ConfigurationVersionId == versionId && r.EntityCode == entityCode)
                .OrderBy(r => r.FieldCode)
                .Select(r => new FieldClassification(r.FieldCode, r.DataClassificationItemId, r.MaskingRule))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
