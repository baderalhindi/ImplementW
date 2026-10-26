using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

/// <summary>
/// ADR-014: a project already under way starts its progress from an opening position entered once at intake, not
/// reconstructed. WF-02 writes it as the opening submission of a one-day period on the intake date, SUBMITTED by the person
/// who recorded the intake, and it is reviewed and published like any other: published progress remains governed. TASK-104
/// calls this from its <c>ProjectIntakeRecorded</c> consumer (§8.2 edge 35; progress-update.md F-6).
/// </summary>
internal sealed class ProgressOpeningPosition(IProgressRepository repository, IProjectFactsReader projects, IAuditTrail audit, TimeProvider timeProvider)
{
    /// <summary>True when recorded; false when this intake's opening position already was, so a redelivered event changes nothing.</summary>
    /// <exception cref="InvalidOperationException">The project does not exist or was not taken in on that date.</exception>
    public async Task<bool> RecordAsync(OpeningPosition position, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (position.OpeningPercentComplete is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position.OpeningPercentComplete, "The opening percent complete is a percentage, 0–100.");
        }

        ProjectFacts project = await projects.FindAsync(position.ProjectId, cancellationToken).ConfigureAwait(false) is { } found && found.LegacyIntakeDate == position.IntakeDate
            ? found
            : throw new InvalidOperationException($"Project {position.ProjectId} was not taken in on {position.IntakeDate}.");
        if (await repository.HasOpeningPositionAsync(position.ProjectIntakeId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid by = position.RecordedByUserId;
        ReportingCycle cycle = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            PeriodStart = position.IntakeDate,
            PeriodEnd = position.IntakeDate,
            DueDate = position.IntakeDate,
            Status = ReportingCycleStatus.Open,
            CreatedAt = now,
            CreatedBy = by,
            UpdatedAt = now,
            UpdatedBy = by,
        };
        ProgressSubmission submission = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            ReportingCycleId = cycle.Id,
            RevisionNo = 1,
            Status = ProgressSubmissionStatus.Submitted,
            ActualPercentCalculated = position.OpeningPercentComplete,
            ProjectIntakeId = position.ProjectIntakeId,
            SubmittedByUserId = by,
            SubmittedAt = now,
            CreatedAt = now,
            CreatedBy = by,
            UpdatedAt = now,
            UpdatedBy = by,
        };
        repository.Add(cycle);
        repository.Add(submission);
        audit.Stage(ProgressAudit.CycleCreated(by, project, cycle));
        audit.Stage(ProgressAudit.OpeningPositionRecorded(by, project, submission));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            ProgressSaveOutcome.Saved => true,
            ProgressSaveOutcome.Duplicate => false,
            ProgressSaveOutcome.ConcurrencyConflict => throw new InvalidOperationException($"Recording the opening position of intake {position.ProjectIntakeId} met a concurrent change."),
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }
}

/// <summary>The opening position of a <c>ProjectIntake</c> (ERD <c>project.project_intake.opening_percent_complete</c>).</summary>
internal sealed record OpeningPosition(Guid ProjectId, Guid ProjectIntakeId, DateOnly IntakeDate, decimal OpeningPercentComplete, Guid RecordedByUserId);
