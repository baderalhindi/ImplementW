using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Domain.Risk;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Application.Features.Risk;

/// <summary>
/// The representations of WF-06's rows. A rating is always read from the matrix version its assessment pinned, by the rating
/// row id stored with it (ERD §7 row 12), never from the version in force now: publishing the matrix again changes no recorded
/// rating. Scoped: each pinned version's ratings are read once per request.
/// </summary>
internal sealed class RiskViews(IRiskRepository repository, IConfigurationResolver configuration, IRiskIssueMaterialisation issues)
{
    private readonly Dictionary<Guid, IReadOnlyList<RiskRatingReference>> _ratings = [];

    public async Task<RiskDetail> DetailAsync(RiskEntity risk, CancellationToken cancellationToken) =>
        (await DetailsAsync([risk], cancellationToken).ConfigureAwait(false))[0];

    /// <summary>The risks in their order, each with its current assessment and the expiry of its ACTIVE acceptance.</summary>
    public async Task<IReadOnlyList<RiskDetail>> DetailsAsync(IReadOnlyList<RiskEntity> risks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(risks);
        Guid[] ids = [.. risks.Select(r => r.Id)];
        Dictionary<Guid, RiskAssessmentVersion> latest = (await repository.ListLatestAssessmentsAsync(ids, cancellationToken).ConfigureAwait(false)).ToDictionary(a => a.RiskId);
        Dictionary<Guid, RiskAcceptance> accepted = (await repository.ListActiveAcceptancesAsync(ids, cancellationToken).ConfigureAwait(false)).ToDictionary(a => a.RiskId);

        // Only risks with a materialisation can have an issue; the others cost WF-07 no query.
        Guid[] materialised = [.. risks.Where(r => r.MaterialisedAt is not null).Select(r => r.Id)];
        ILookup<Guid, Guid> raised = (materialised.Length == 0 ? [] : await issues.ListByOriginatingRisksAsync(materialised, cancellationToken).ConfigureAwait(false))
            .ToLookup(i => i.OriginatingRiskId, i => i.Id);
        List<RiskDetail> details = [];
        foreach (RiskEntity r in risks)
        {
            RiskAssessmentSummary? current = latest.TryGetValue(r.Id, out RiskAssessmentVersion? a)
                ? new RiskAssessmentSummary(a.Id, a.VersionNo, a.AssessedAt, a.MatrixConfigurationVersionId, a.ProbabilityLevel, a.OverallImpactLevel,
                    await RatingOfAsync(a, cancellationToken).ConfigureAwait(false))
                : null;
            details.Add(new RiskDetail(
                r.Id, r.ProjectId, r.Title, r.Description, r.RiskCategoryItemId, r.OwnerUserId, r.Status, r.IdentifiedDate, r.NextReviewDate, current,
                accepted.TryGetValue(r.Id, out RiskAcceptance? acceptance) ? acceptance.ExpiresOn : null,
                r.MaterialisedAt, [.. raised[r.Id]], r.ClosureRationale, r.ClosedAt, r.ClosedByUserId, r.ReopenedCount, r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy));
        }

        return details;
    }

    /// <summary>The assessment versions in their order, each with its impacts and its recorded rating.</summary>
    public async Task<IReadOnlyList<RiskAssessmentDetail>> AssessmentDetailsAsync(IReadOnlyList<RiskAssessmentVersion> assessments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assessments);
        ILookup<Guid, RiskAssessmentImpact> impacts = (await repository.ListImpactsAsync([.. assessments.Select(a => a.Id)], cancellationToken).ConfigureAwait(false))
            .ToLookup(i => i.RiskAssessmentVersionId);
        List<RiskAssessmentDetail> details = [];
        foreach (RiskAssessmentVersion a in assessments)
        {
            details.Add(new RiskAssessmentDetail(
                a.Id, a.RiskId, a.VersionNo, a.AssessedAt, a.AssessedByUserId, a.MatrixConfigurationVersionId, a.ProbabilityLevel, a.OverallImpactLevel,
                await RatingOfAsync(a, cancellationToken).ConfigureAwait(false),
                [.. impacts[a.Id].OrderBy(i => i.ImpactDimensionItemId).Select(i => new RiskAssessmentImpactDetail(i.ImpactDimensionItemId, i.ImpactLevel, i.Rationale))],
                a.Rationale));
        }

        return details;
    }

    public static RiskTreatmentActionDetail ToDetail(RiskTreatmentAction a) =>
        new(a.Id, a.RiskId, a.Title, a.Description, a.ActionType, a.OwnerUserId, a.DueDate, a.Status, a.CompletedAt, a.CreatedAt, a.CreatedBy, a.UpdatedAt, a.UpdatedBy);

    public static RiskAcceptanceDetail ToDetail(RiskAcceptance a) =>
        new(a.Id, a.RiskId, a.AcceptedByUserId, a.AcceptedAt, a.ExpiresOn, a.Rationale, a.Status, a.RevokedAt, a.UpdatedAt);

    /// <summary>The rating the assessment recorded, labelled as its pinned matrix version labels it.</summary>
    private async Task<RiskRatingDetail> RatingOfAsync(RiskAssessmentVersion assessment, CancellationToken cancellationToken)
    {
        if (!_ratings.TryGetValue(assessment.MatrixConfigurationVersionId, out IReadOnlyList<RiskRatingReference>? ratings))
        {
            ratings = await configuration.ListRiskRatingsAsync(assessment.MatrixConfigurationVersionId, cancellationToken).ConfigureAwait(false);
            _ratings[assessment.MatrixConfigurationVersionId] = ratings;
        }

        RiskRatingReference rating = ratings.SingleOrDefault(r => r.Id == assessment.RiskRatingDefinitionId)
                                     ?? throw new ConfigurationMissingException(ConfigurationFamilyCodes.RiskMatrix, ConfigurationMissingReason.EntryMissing, $"risk rating {assessment.RiskRatingDefinitionId}");
        return new RiskRatingDetail(rating.Id, rating.Code, rating.Label);
    }
}
