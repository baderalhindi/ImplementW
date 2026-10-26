using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>Reads what <see cref="ConfigurationContentRules"/> checks a content against, in a fixed number of queries.</summary>
internal sealed class ConfigurationReferenceReader(IMasterDataRepository masterData, IRoleDirectory roles)
{
    /// <summary>The catalogues whose every PUBLISHED value some family must cover (<see cref="ConfigurationContentRules.CheckComplete"/>).</summary>
    private static readonly string[] CoveredCatalogues =
        [MasterDataCatalogueCodes.ImpactDimension, MasterDataCatalogueCodes.GovernanceProfile, MasterDataCatalogueCodes.ContributionType];

    public async Task<ConfigurationReferences> ReadAsync(ConfigurationContent content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        HashSet<Guid> itemIds =
        [
            .. content.GovernanceProfiles.SelectMany(p => new[] { p.GovernanceProfileItemId, p.DocumentControlLevelItemId }),
            .. content.MaterialityBands.Select(b => b.GovernanceProfileItemId),
            .. content.ImpactLevels.Select(l => l.ImpactDimensionItemId),
            .. content.ApprovalAuthority.Where(r => r.GovernanceProfileItemId is not null).Select(r => r.GovernanceProfileItemId!.Value),
            .. content.ParticipationRules.Select(r => r.ContributionTypeItemId),
            .. content.EvidenceRequirements.SelectMany(r => new[] { r.MilestoneCategoryItemId, r.EvidenceTypeItemId }),
            .. content.FieldClassifications.Select(r => r.DataClassificationItemId),
            .. content.ReportFields.Where(f => f.DataClassificationItemId is not null).Select(f => f.DataClassificationItemId!.Value),
        ];
        HashSet<Guid> kpiDefinitionIds = [.. content.KpiPolicies.Select(p => p.KpiDefinitionId)];

        Dictionary<string, IReadOnlyList<Guid>> published = new(StringComparer.Ordinal);
        foreach (string catalogue in CoveredCatalogues)
        {
            published[catalogue] = [.. (await masterData.ListPublishedItemsAsync(catalogue, cancellationToken).ConfigureAwait(false)).Select(i => i.Id)];
        }

        return new ConfigurationReferences(
            itemIds.Count == 0 ? new Dictionary<Guid, ItemFacts>() : await masterData.FindItemFactsAsync(itemIds, cancellationToken).ConfigureAwait(false),
            kpiDefinitionIds.Count == 0 ? new Dictionary<Guid, Domain.Common.GovernedLifecycleState>()
                : await masterData.FindKpiDefinitionStatesAsync(kpiDefinitionIds, cancellationToken).ConfigureAwait(false),
            new HashSet<Guid>((await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).Select(r => r.Id)),
            published);
    }
}
