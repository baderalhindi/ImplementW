using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

/// <summary>
/// Generates a project's reporting periods from its governance profile's cadence (progress-update.md D-5), when someone reports. WF-02 and
/// WF-14 report on the same periods, so whichever is reported first generates them (<see cref="IReportingPeriodSource"/>).
/// </summary>
internal sealed class ReportingPeriods(IProgressRepository repository, ProgressPolicy policy, IAuditTrail audit)
{
    /// <summary>
    /// The periods due up to today, staged with this unit of work; with those already generated, earliest first. The cadence is resolved only
    /// when a period is due.
    /// </summary>
    public async Task<IReadOnlyList<ReportingCycle>> StageDueAsync(Guid callerId, ProjectFacts project, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        List<ReportingCycle> cycles = [.. await repository.ListCyclesAsync(project.Id, cancellationToken).ConfigureAwait(false)];
        if (ReportingCalendar.FirstPeriodStart(project) is not { } first)
        {
            return cycles;
        }

        DateOnly today = ReportingCalendar.Today(now);
        DateOnly next = ReportingCalendar.NextPeriodStart(first, cycles.Count == 0 ? null : cycles.Max(c => c.PeriodEnd));
        if (next > today)
        {
            return cycles;
        }

        int cadenceDays = await policy.UpdateCadenceDaysAsync(project.GovernanceProfileItemId, now, cancellationToken).ConfigureAwait(false);
        foreach ((DateOnly start, DateOnly end) in ReportingCalendar.PeriodsBegunBy(next, cadenceDays, today))
        {
            ReportingCycle cycle = new()
            {
                Id = Guid.CreateVersion7(now),
                ProjectId = project.Id,
                PeriodStart = start,
                PeriodEnd = end,
                DueDate = end,
                Status = ReportingCycleStatus.Open,
                CreatedAt = now,
                CreatedBy = callerId,
                UpdatedAt = now,
                UpdatedBy = callerId,
            };
            repository.Add(cycle);
            audit.Stage(ProgressAudit.CycleCreated(callerId, project, cycle));
            cycles.Add(cycle);
        }

        return cycles;
    }
}

/// <inheritdoc cref="IReportingPeriodSource"/>
internal sealed class ReportingPeriodSource(ReportingPeriods periods, TimeProvider timeProvider) : IReportingPeriodSource
{
    public async Task<IReadOnlyList<ReportingCycleSummary>> StageDueAsync(Guid callerId, ProjectFacts project, CancellationToken cancellationToken) =>
        [.. (await periods.StageDueAsync(callerId, project, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false)).Select(ProgressMapping.ToSummary)];
}
