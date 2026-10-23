using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.Contracts.Events;

namespace PMPlatform.Application.Features.Progress.EventHandlers;

/// <summary>ADR-014, edge 35: WF-02 writes the intake's opening percentage as the one-day period's SUBMITTED revision (progress-update.md D-7).</summary>
internal sealed class ProgressIntakeHandler(ProgressOpeningPosition openingPosition) : IProjectIntakeHandler
{
    /// <summary>Before FinancialKpi, whose opening spend sits in the period this writes.</summary>
    public int Order => 20;

    public Task HandleAsync(ProjectIntakeRecorded intake, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intake);
        ProjectIntakeRecordedData data = intake.Data;
        return openingPosition.RecordAsync(
            new OpeningPosition(data.ProjectId, data.ProjectIntakeId, data.LegacyIntakeDate, data.OpeningPosition.PercentComplete, data.RecordedByUserId),
            cancellationToken);
    }
}
