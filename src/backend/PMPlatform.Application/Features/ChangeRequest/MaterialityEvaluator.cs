using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.Common;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>
/// Evaluates a request's revision now (ADR-016): the governed commitments it changes as they stand — the ACTIVE Approved Baseline
/// (WF-03, through <see cref="IApprovedBaselineSource"/>) and, for a cost impact, the ACTIVE Approved Budget (WF-14, through
/// <see cref="IApprovedBudgetSource"/>) — the approved changes since that baseline, and the bands of the project's governance profile in
/// the MATERIALITY_BAND version in force. The result pins all of them. Nothing is recorded here: the review records it, a preview does not.
/// </summary>
internal sealed class MaterialityEvaluator(
    IChangeRequestRepository repository, IConfigurationResolver configuration, IApprovedBaselineSource baselines, IApprovedBudgetSource budgets)
{
    /// <summary>
    /// The evaluation, or 422 <c>CHANGE_REQUEST_TARGET_UNAVAILABLE</c> when a commitment the request changes does not exist: missing
    /// is never read as zero (WF-08 BR-CHG-031). No version in force, or no three bands for the profile, fails closed (422 CONFIGURATION_MISSING).
    /// </summary>
    public async Task<AdministrationResult<MaterialityEvaluation>> EvaluateAsync(
        ProjectFacts project, ChangeRequestEntity request, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        (ApprovedBaselineFacts? baseline, ApprovedBudgetFacts? budget, AdministrationError? unavailable) = await TargetsAsync(project, request, cancellationToken).ConfigureAwait(false);
        if (unavailable is not null)
        {
            return unavailable;
        }

        ResolvedConfiguration version = await configuration.ResolveAsync(ConfigurationFamilyCodes.MaterialityBand, now, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<MaterialityBandEntry> bands = version.RequireMaterialityBands(project.GovernanceProfileItemId);

        IReadOnlyList<(Money? CostImpactSar, int? ScheduleImpactDays)> approved =
            await repository.ListApprovedImpactsAsync(project.Id, baseline?.BaselineId, request.Id, cancellationToken).ConfigureAwait(false);
        Money cumulativeCost = approved.Aggregate(request.CostImpactSar ?? Money.Zero, (sum, change) => sum + (change.CostImpactSar ?? Money.Zero));
        int cumulativeDays = approved.Sum(change => change.ScheduleImpactDays ?? 0) + (request.ScheduleImpactDays ?? 0);

        MaterialityBands result = ChangeMateriality.Classify(bands, new MaterialityInputs(
            cumulativeCost, request.CostImpactSar is not null, budget?.AmountSar,
            cumulativeDays, request.ScheduleImpactDays is not null, baseline?.DurationDays,
            request.ScopeImpact is not null, request.IsContractualObligation));

        return new MaterialityEvaluation
        {
            Id = Guid.CreateVersion7(now),
            ChangeRequestId = request.Id,
            RevisionNo = request.RevisionNo,
            EvaluatedAt = now,
            MaterialityConfigurationVersionId = version.VersionId,
            ProjectBaselineId = baseline?.BaselineId,
            ProjectBaselineVersionNo = baseline?.VersionNo,
            BaselineDurationDays = baseline?.DurationDays,
            FinancialCommitmentId = budget?.CommitmentId,
            FinancialCommitmentVersionNo = budget?.VersionNo,
            BaselineBudgetSar = budget?.AmountSar,
            CumulativeCostImpactSar = cumulativeCost,
            CumulativeScheduleImpactDays = cumulativeDays,
            CostBandNo = result.Cost,
            ScheduleBandNo = result.Schedule,
            ScopeBandNo = result.Scope,
            ResultingBandNo = result.Resulting,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
    }

    /// <summary>
    /// 422 <c>CHANGE_REQUEST_TARGET_UNAVAILABLE</c> when a commitment the request changes does not exist now: a schedule impact without an
    /// ACTIVE Approved Baseline, a cost impact without an ACTIVE Approved Budget. Submission asks, so the requester learns it first.
    /// </summary>
    public async Task<AdministrationError?> TargetsRefusedAsync(ProjectFacts project, ChangeRequestEntity request, CancellationToken cancellationToken) =>
        (await TargetsAsync(project, request, cancellationToken).ConfigureAwait(false)).Unavailable;

    /// <summary>The classification as the API states it; a preview has no evaluation id.</summary>
    public static MaterialityAssessment ToAssessment(MaterialityEvaluation e, bool recorded)
    {
        ArgumentNullException.ThrowIfNull(e);
        return new MaterialityAssessment(
            recorded ? e.Id : null, e.RevisionNo, e.EvaluatedAt, e.MaterialityConfigurationVersionId, e.ProjectBaselineId, e.FinancialCommitmentId,
            e.CumulativeCostImpactSar, e.CumulativeScheduleImpactDays, e.CostBandNo, e.ScheduleBandNo, e.ScopeBandNo, e.ResultingBandNo);
    }

    /// <summary>The ACTIVE Approved Baseline, if any, and, for a cost impact, the ACTIVE Approved Budget, each required when its impact is stated.</summary>
    private async Task<(ApprovedBaselineFacts? Baseline, ApprovedBudgetFacts? Budget, AdministrationError? Unavailable)> TargetsAsync(
        ProjectFacts project, ChangeRequestEntity request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        ApprovedBaselineFacts? baseline = await baselines.FindActiveAsync(project.Id, cancellationToken).ConfigureAwait(false);
        ApprovedBudgetFacts? budget = request.CostImpactSar is null ? null : await budgets.FindActiveAsync(project.Id, cancellationToken).ConfigureAwait(false);
        AdministrationError? unavailable =
            request.ScheduleImpactDays is not null && baseline is null ? AdministrationError.Rule(ChangeRequestErrorCodes.TargetUnavailable, new FieldIssue("scheduleImpactDays", FieldIssue.NotFound))
            : request.CostImpactSar is not null && budget is null ? AdministrationError.Rule(ChangeRequestErrorCodes.TargetUnavailable, new FieldIssue("costImpactSar", FieldIssue.NotFound))
            : null;
        return (baseline, budget, unavailable);
    }
}
