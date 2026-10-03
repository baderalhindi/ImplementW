using PMPlatform.Application.Common.Auditing;
using PMPlatform.Domain.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>
/// Keeps the project's Activity Execution Progress (ADR-009) in step with its tasks, inside the write that moved them: every
/// activity a task executes against, or that has a row already, is rolled up again from the board, and a row is written, and
/// audited, only when its figure changed. An activity left with no live leaf task keeps its row at 0: no work is recorded against it.
/// </summary>
internal sealed class ActivityProgressProjection(IProjectTaskRepository repository, IAuditTrail audit)
{
    public async Task StageAsync(Guid actorId, TaskBoard board, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(board);
        Dictionary<Guid, ActivityExecutionProgress> rows =
            (await repository.ListActivityProgressAsync(board.Project.Id, cancellationToken).ConfigureAwait(false)).ToDictionary(p => p.ScheduleActivityId);
        IEnumerable<Guid> activities = board.Tasks.Where(t => t.ScheduleActivityId is not null).Select(t => t.ScheduleActivityId!.Value).Concat(rows.Keys).Distinct();
        foreach (Guid activityId in activities)
        {
            decimal? percent = TaskProgressRollup.ActivityPercent(activityId, board.Tasks);
            if (!rows.TryGetValue(activityId, out ActivityExecutionProgress? row))
            {
                if (percent is null)
                {
                    continue;
                }

                row = new ActivityExecutionProgress
                {
                    Id = Guid.CreateVersion7(now),
                    ProjectId = board.Project.Id,
                    ScheduleActivityId = activityId,
                    ActualPercentComplete = percent.Value,
                    ComputedAt = now,
                    CreatedAt = now,
                    CreatedBy = actorId,
                    UpdatedAt = now,
                    UpdatedBy = actorId,
                };
                repository.Add(row);
                audit.Stage(ProjectTaskAudit.ProgressRecomputed(actorId, board.Project, row, null));
                continue;
            }

            decimal before = row.ActualPercentComplete;
            if (before == (percent ?? 0m))
            {
                continue;
            }

            row.ActualPercentComplete = percent ?? 0m;
            row.ComputedAt = now;
            row.UpdatedAt = now;
            row.UpdatedBy = actorId;
            audit.Stage(ProjectTaskAudit.ProgressRecomputed(actorId, board.Project, row, before));
        }
    }
}
