using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.Project;

/// <summary>
/// What a registration names, checked as it is written (E-U1, E-U2): PUBLISHED master data items of the right catalogue,
/// an active department and entity, and a participation mode that has an entity to manage it. A strategic initiative is one of the chosen
/// strategic portfolio's (the 2 Nov 2026 review), and the follow-up owner an active AHDA user.
/// </summary>
internal sealed class ProjectReferences(IMasterDataResolver masterData, IOrganizationDirectory organizations, IUserLookup users)
{
    /// <summary>Every issue of the draft, or null if it has none.</summary>
    public async Task<AdministrationError?> CheckAsync(ProjectDraft draft, CancellationToken cancellationToken)
    {
        if (draft.ParticipationMode == ParticipationMode.EntityManaged && draft.ExternalEntityId is null)
        {
            return AdministrationError.Rule(ProjectErrorCodes.ParticipationInvalid, new FieldIssue("externalEntityId", FieldIssue.Required));
        }

        List<FieldIssue> issues = [];
        await RequireItemAsync(MasterDataCatalogueCodes.ProjectClassification, draft.ClassificationItemId, "classificationItemId", issues, cancellationToken).ConfigureAwait(false);
        await RequireItemAsync(MasterDataCatalogueCodes.GovernanceProfile, draft.GovernanceProfileItemId, "governanceProfileItemId", issues, cancellationToken).ConfigureAwait(false);
        if (draft.RegionItemId is { } region)
        {
            await RequireItemAsync(MasterDataCatalogueCodes.Region, region, "regionItemId", issues, cancellationToken).ConfigureAwait(false);
        }

        if (draft.CityItemId is { } city
            && await RequireItemAsync(MasterDataCatalogueCodes.City, city, "cityItemId", issues, cancellationToken).ConfigureAwait(false) is { ParentItemId: { } cityRegion }
            && draft.RegionItemId is { } namedRegion && cityRegion != namedRegion)
        {
            // core-platform-schema N-1 (b): a city lies in one region, so the two may not disagree.
            issues.Add(new FieldIssue("cityItemId", FieldIssue.NotAllowed));
        }

        if (draft.StrategicPortfolioItemId is { } portfolio)
        {
            await RequireItemAsync(MasterDataCatalogueCodes.StrategicPortfolio, portfolio, "strategicPortfolioItemId", issues, cancellationToken).ConfigureAwait(false);
        }

        if (draft.StrategicInitiativeItemId is { } initiative
            && await RequireItemAsync(MasterDataCatalogueCodes.StrategicInitiative, initiative, "strategicInitiativeItemId", issues, cancellationToken).ConfigureAwait(false) is { } named
            && (draft.StrategicPortfolioItemId is not { } chosen || named.ParentItemId != chosen))
        {
            // An initiative belongs to exactly one portfolio, which must be the one chosen.
            issues.Add(new FieldIssue("strategicInitiativeItemId", FieldIssue.NotAllowed));
        }

        if (draft.PriorityItemId is { } priority)
        {
            await RequireItemAsync(MasterDataCatalogueCodes.Priority, priority, "priorityItemId", issues, cancellationToken).ConfigureAwait(false);
        }

        if (draft.FollowUpOwnerUserId is { } owner
            && !(await users.ListActiveAsync(cancellationToken).ConfigureAwait(false)).Any(u => u.Id == owner && u.UserType == UserType.Internal))
        {
            issues.Add(new FieldIssue("followUpOwnerUserId", FieldIssue.NotFound));
        }

        if (!await organizations.IsActiveDepartmentAsync(draft.DepartmentId, cancellationToken).ConfigureAwait(false))
        {
            issues.Add(new FieldIssue("departmentId", FieldIssue.NotFound));
        }

        if (draft.ExternalEntityId is { } entity && !await organizations.IsActiveExternalEntityAsync(entity, cancellationToken).ConfigureAwait(false))
        {
            issues.Add(new FieldIssue("externalEntityId", FieldIssue.NotFound));
        }

        return issues.Count == 0 ? null : AdministrationError.Rule(ProjectErrorCodes.ReferenceInvalid, [.. issues]);
    }

    private async Task<MasterDataItemReference?> RequireItemAsync(string catalogueCode, Guid itemId, string field, List<FieldIssue> issues, CancellationToken cancellationToken)
    {
        try
        {
            return await masterData.RequirePublishedItemAsync(catalogueCode, itemId, cancellationToken).ConfigureAwait(false);
        }
        catch (ConfigurationMissingException)
        {
            issues.Add(new FieldIssue(field, FieldIssue.NotFound));
            return null;
        }
    }
}
