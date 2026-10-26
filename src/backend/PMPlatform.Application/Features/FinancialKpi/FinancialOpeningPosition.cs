using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// ADR-014 for WF-14 (financial-kpi.md F-13): a project taken in starts with the budget it declared, a DECLARED_BUDGET born ACTIVE, and
/// its spend to date as the opening period's update, SUBMITTED by the person who recorded the intake and reviewed and published like any
/// other. The update sits in the one-day period WF-02 writes for the intake (edge 13), so Progress's handler runs first. Recorded once per
/// intake: a redelivered intake changes nothing.
/// </summary>
internal sealed class FinancialOpeningPosition(
    IFinancialKpiRepository repository, IProjectFactsReader projects, IReportingCycleReader cycles, IAuditTrail audit, TimeProvider timeProvider)
{
    /// <summary>True when recorded; false when this intake's opening position already was.</summary>
    /// <exception cref="InvalidOperationException">The project was not taken in on that date, or its opening period does not exist yet.</exception>
    public async Task<bool> RecordAsync(ProjectIntakeRecordedData intake, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intake);

        ProjectFacts project = await projects.FindAsync(intake.ProjectId, cancellationToken).ConfigureAwait(false) is { } found && found.LegacyIntakeDate == intake.LegacyIntakeDate
            ? found
            : throw new InvalidOperationException($"Project {intake.ProjectId} was not taken in on {intake.LegacyIntakeDate}.");
        IReadOnlyList<FinancialCommitment> declared = await repository.ListCommitmentsAsync(project.Id, CommitmentType.DeclaredBudget, cancellationToken).ConfigureAwait(false);
        if (declared.Any(c => c.ProjectIntakeId == intake.ProjectIntakeId))
        {
            return false;
        }

        ReportingCycleSummary cycle = (await cycles.ListAsync(project.Id, cancellationToken).ConfigureAwait(false))
                                      .FirstOrDefault(c => c.PeriodStart == intake.LegacyIntakeDate && c.PeriodEnd == intake.LegacyIntakeDate)
                                      ?? throw new InvalidOperationException($"The opening period of intake {intake.ProjectIntakeId} does not exist yet.");
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid by = intake.RecordedByUserId;
        FinancialCommitment budget = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            CommitmentType = CommitmentType.DeclaredBudget,
            VersionNo = declared.Count == 0 ? 1 : declared.Max(c => c.VersionNo) + 1,
            Status = ApprovedVersionStatus.Active,
            AmountSar = new Money(intake.DeclaredBaseline.BudgetSar),
            EffectiveFrom = intake.LegacyIntakeDate,
            ActivatedAt = now,
            ProjectIntakeId = intake.ProjectIntakeId,
            SourceType = FinancialSourceType.Manual,
            AsOfDate = intake.LegacyIntakeDate,
            EnteredByUserId = by,
            CreatedAt = now,
            CreatedBy = by,
            UpdatedAt = now,
            UpdatedBy = by,
        };
        FinancialProgressUpdate spend = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            ReportingCycleId = cycle.Id,
            RevisionNo = 1,
            Status = FinancialUpdateStatus.Submitted,
            ActualExpenditureToDateSar = new Money(intake.OpeningPosition.SpendToDateSar),
            ValueStatus = ValueStatus.Measured,
            ProjectIntakeId = intake.ProjectIntakeId,
            SourceType = FinancialSourceType.Manual,
            AsOfDate = intake.LegacyIntakeDate,
            EnteredByUserId = by,
            SubmittedByUserId = by,
            SubmittedAt = now,
            CreatedAt = now,
            CreatedBy = by,
            UpdatedAt = now,
            UpdatedBy = by,
        };
        repository.Add(budget);
        repository.Add(spend);
        audit.Stage(FinancialKpiAudit.DeclaredBudgetRecorded(by, project, budget));
        audit.Stage(FinancialKpiAudit.OpeningSpendRecorded(by, project, spend));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            FinancialKpiSaveOutcome.Saved => true,
            FinancialKpiSaveOutcome.Duplicate => false,
            FinancialKpiSaveOutcome.ConcurrencyConflict => throw new InvalidOperationException($"Recording the opening position of intake {intake.ProjectIntakeId} met a concurrent change."),
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }
}
