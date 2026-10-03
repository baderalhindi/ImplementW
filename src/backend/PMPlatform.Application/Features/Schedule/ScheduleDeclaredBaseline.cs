using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// ADR-014: a project already under way enters with a Declared Baseline — its end date and scope as declared at intake, no
/// retrospective plan — first-class and distinguishable from an Approved Baseline. WF-03 writes it born ACTIVE, so it meets
/// ADR-009's activation precondition for the intake project; the project's first APPROVED baseline supersedes it like any
/// other. TASK-104 calls this from its <c>ProjectIntakeRecorded</c> consumer (§8.2 edge 35; schedule-baseline.md F-6).
/// </summary>
internal sealed class ScheduleDeclaredBaseline(
    IScheduleRepository repository, IProjectFactsReader projects, ScheduleHealthProjection health, IAuditTrail audit, TimeProvider timeProvider)
{
    /// <summary>True when recorded; false when this intake's baseline already was, so a redelivered event changes nothing.</summary>
    /// <exception cref="InvalidOperationException">The project does not exist, was not taken in on that date, or already has an ACTIVE baseline.</exception>
    public async Task<bool> RecordAsync(DeclaredBaselineIntake intake, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intake);

        ProjectFacts project = await projects.FindAsync(intake.ProjectId, cancellationToken).ConfigureAwait(false) is { } found && found.LegacyIntakeDate == intake.IntakeDate
            ? found
            : throw new InvalidOperationException($"Project {intake.ProjectId} was not taken in on {intake.IntakeDate}.");
        if (await repository.HasDeclaredBaselineAsync(intake.ProjectIntakeId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await using IScheduleWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        ProjectSchedule? schedule = await repository.LockScheduleAsync(project.Id, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ProjectBaseline> baselines = await repository.ListBaselinesAsync(project.Id, track: false, cancellationToken).ConfigureAwait(false);
        if (baselines.Any(b => b.Status == ProjectBaselineStatus.Active))
        {
            throw new InvalidOperationException($"Project {project.Id} already has an ACTIVE baseline; a Declared Baseline is its first.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid by = intake.RecordedByUserId;
        ProjectBaseline baseline = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            BaselineType = BaselineType.Declared,
            VersionNo = baselines.Count == 0 ? 1 : baselines.Max(b => b.VersionNo) + 1,
            RevisionNo = 1,
            Status = ProjectBaselineStatus.Active,
            ActivatedAt = now,
            ProjectIntakeId = intake.ProjectIntakeId,
            DeclaredEndDate = intake.DeclaredEndDate,
            DeclaredScope = intake.DeclaredScope,
            BaselineFinishDate = intake.DeclaredEndDate,
            CreatedAt = now,
            CreatedBy = by,
            UpdatedAt = now,
            UpdatedBy = by,
        };
        repository.Add(baseline);
        audit.Stage(ScheduleAudit.DeclaredBaselineRecorded(by, project, baseline));
        IReadOnlyList<ScheduleActivity> activities = schedule is null ? [] : await repository.ListActivitiesAsync(schedule.Id, track: false, cancellationToken).ConfigureAwait(false);
        await health.StageAsync(by, project, activities, baseline, now, cancellationToken).ConfigureAwait(false);
        switch (await repository.SaveAsync(cancellationToken).ConfigureAwait(false))
        {
            case ScheduleSaveOutcome.Saved:
                await work.CommitAsync(cancellationToken).ConfigureAwait(false);
                return true;
            case ScheduleSaveOutcome.Duplicate:
                return false;
            case ScheduleSaveOutcome.ConcurrencyConflict:
                throw new InvalidOperationException($"Recording the Declared Baseline of intake {intake.ProjectIntakeId} met a concurrent change.");
            default:
                throw new InvalidOperationException("Unknown save outcome.");
        }
    }
}

/// <summary>What a <c>ProjectIntake</c> declares for the schedule (ERD <c>project.project_intake</c>).</summary>
internal sealed record DeclaredBaselineIntake(Guid ProjectId, Guid ProjectIntakeId, DateOnly IntakeDate, DateOnly DeclaredEndDate, NarrativeText DeclaredScope, Guid RecordedByUserId);
