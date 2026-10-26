using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Domain.Risk;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Application.Features.Risk;

/// <summary>Edge 29: open risks by the rating their latest assessment recorded, read from the matrix version that assessment pinned.</summary>
internal sealed class RiskExposureReader(IRiskRepository repository, IConfigurationResolver configuration) : IRiskExposureReader
{
    public async Task<IReadOnlyList<RiskExposure>> ListAsync(IReadOnlyList<Guid> projectIds, DateOnly today, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        IReadOnlyList<RiskEntity> open = await repository.ListOpenRisksAsync(projectIds, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, RiskAssessmentVersion> latest = (await repository.ListLatestAssessmentsAsync([.. open.Select(r => r.Id)], cancellationToken).ConfigureAwait(false))
            .ToDictionary(a => a.RiskId);

        Dictionary<Guid, IReadOnlyList<RiskRatingReference>> matrices = [];
        foreach (Guid versionId in latest.Values.Select(a => a.MatrixConfigurationVersionId).Distinct())
        {
            matrices[versionId] = await configuration.ListRiskRatingsAsync(versionId, cancellationToken).ConfigureAwait(false);
        }

        ILookup<Guid, RiskEntity> byProject = open.ToLookup(r => r.ProjectId);
        return [.. projectIds.Select(projectId =>
        {
            List<RiskEntity> risks = [.. byProject[projectId]];
            List<RiskRatingReference> ratings = [.. risks.Where(r => latest.ContainsKey(r.Id)).Select(r => RatingOf(latest[r.Id], matrices))];
            return new RiskExposure(
                projectId,
                risks.Count,
                risks.Count(r => !latest.ContainsKey(r.Id)),
                risks.Count(r => r.NextReviewDate < today),
                [.. ratings.GroupBy(r => r.Code, StringComparer.Ordinal)
                    .Select(g => new RiskRatingCount(g.Key, g.First().Label, g.First().SortOrder, g.Count()))
                    .OrderBy(c => c.SortOrder).ThenBy(c => c.RatingCode, StringComparer.Ordinal)]);
        })];
    }

    /// <summary>The rating the assessment recorded; a rating its pinned version no longer lists is a configuration fault, as in <see cref="RiskViews"/>.</summary>
    private static RiskRatingReference RatingOf(RiskAssessmentVersion assessment, Dictionary<Guid, IReadOnlyList<RiskRatingReference>> matrices) =>
        matrices[assessment.MatrixConfigurationVersionId].SingleOrDefault(r => r.Id == assessment.RiskRatingDefinitionId)
        ?? throw new ConfigurationMissingException(ConfigurationFamilyCodes.RiskMatrix, ConfigurationMissingReason.EntryMissing, $"risk rating {assessment.RiskRatingDefinitionId}");
}
