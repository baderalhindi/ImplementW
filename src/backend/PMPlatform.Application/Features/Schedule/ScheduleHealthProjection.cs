using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// Rewrites a project's CURRENT/LIVE Schedule Health from the forecast as it now stands and the ACTIVE baseline, staged
/// with the caller's unit of work, after every change that can move either (TASK-046). It is the only writer of the row.
/// </summary>
internal sealed class ScheduleHealthProjection(IScheduleRepository repository, SchedulePolicy policy, IAuditTrail audit)
{
    public async Task StageAsync(
        Guid actorId, ProjectFacts project, IReadOnlyList<ScheduleActivity> activities, ProjectBaseline? activeBaseline, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);

        int? variance = ScheduleVariance.ProjectFinish(activeBaseline, ScheduleCalculation.ForecastFinish(activities));
        ScheduleHealthRuleVersion? rule = await policy.HealthRuleAsync(now, cancellationToken).ConfigureAwait(false);
        ScheduleHealth rating = ScheduleHealthRule.Rate(variance, rule?.Thresholds);

        ScheduleHealthStatus? health = await repository.FindHealthStatusAsync(project.Id, track: true, cancellationToken).ConfigureAwait(false);
        (ScheduleHealth? before, int? varianceBefore, Guid? baselineBefore) = (health?.ScheduleHealth, health?.FinishVarianceDays, health?.ProjectBaselineId);
        if (health is null)
        {
            health = new ScheduleHealthStatus { Id = Guid.CreateVersion7(now), ProjectId = project.Id, CreatedAt = now, CreatedBy = actorId };
            repository.Add(health);
        }

        health.ProjectBaselineId = activeBaseline?.Id;
        health.ScheduleHealth = rating;
        health.FinishVarianceDays = variance;
        health.HealthRuleConfigurationVersionId = rule?.ConfigurationVersionId;
        health.ComputedAt = now;
        health.UpdatedAt = now;
        health.UpdatedBy = actorId;

        // Audited when what a reader sees changes, not on every recompute.
        if ((before, varianceBefore, baselineBefore) != (rating, variance, activeBaseline?.Id))
        {
            audit.Stage(ScheduleAudit.HealthRecomputed(actorId, project, health, before, varianceBefore));
        }
    }
}
