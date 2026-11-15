using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Project.FollowUp;

/// <inheritdoc cref="IManagedProjectAttentionService"/>
/// <remarks>
/// The projects are those PROJECT_VIEW reaches for the caller that name them as manager, read in one query; each kind of item is then read for
/// all of them at once, so a manager of many projects costs four queries, not one per project.
/// </remarks>
internal sealed class ManagedProjectAttentionService(
    IAuthorizationEngine authorization,
    IProjectFactsReader projects,
    IReportingCycleReader cycles,
    IOverdueTaskCounter tasks,
    IOverdueMilestoneCounter milestones,
    TimeProvider timeProvider) : IManagedProjectAttentionService
{
    public async Task<ManagedProjectAttentionList> ListAsync(Guid callerId, CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        DateOnly monthEnd = new(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        RecordScope scope = await authorization.GetRecordScopeAsync(callerId, PermissionCatalogue.ProjectView, cancellationToken).ConfigureAwait(false);
        List<Guid> managed = [.. (await projects.ListReachedAsync(scope, cancellationToken).ConfigureAwait(false))
            .Where(p => p.ProjectManagerUserId == callerId)
            .Select(p => p.Id)];
        if (managed.Count == 0)
        {
            return new ManagedProjectAttentionList(today, monthEnd, []);
        }

        ILookup<Guid, ReportingCycleSummary> due = (await cycles.ListAsync(managed, cancellationToken).ConfigureAwait(false))
            .Where(c => c.Status == ReportingCycleStatus.Open && c.DueDate <= monthEnd)
            .ToLookup(c => c.ProjectId);
        IReadOnlyDictionary<Guid, int> overdueTasks = await tasks.CountAsync(managed, today, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, int> overdueMilestones = await milestones.CountAsync(managed, today, cancellationToken).ConfigureAwait(false);
        return new ManagedProjectAttentionList(today, monthEnd, [.. managed.Select(id => new ManagedProjectAttention(
            id,
            due[id].Min(c => (DateOnly?)c.DueDate),
            overdueTasks.GetValueOrDefault(id),
            overdueMilestones.GetValueOrDefault(id)))]);
    }
}
