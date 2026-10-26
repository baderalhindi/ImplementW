using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.Contracts.Events;

namespace PMPlatform.Application.Features.Schedule.EventHandlers;

/// <summary>ADR-014, edge 35: WF-03 writes the intake's Declared Baseline, born ACTIVE (schedule-baseline.md F-6).</summary>
internal sealed class ScheduleIntakeHandler(ScheduleDeclaredBaseline declaredBaseline) : IProjectIntakeHandler
{
    public int Order => 10;

    public Task HandleAsync(ProjectIntakeRecorded intake, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intake);
        ProjectIntakeRecordedData data = intake.Data;
        return declaredBaseline.RecordAsync(
            new DeclaredBaselineIntake(data.ProjectId, data.ProjectIntakeId, data.LegacyIntakeDate, data.DeclaredBaseline.EndDate, data.DeclaredBaseline.Scope, data.RecordedByUserId),
            cancellationToken);
    }
}
