using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.Contracts.Events;

namespace PMPlatform.Application.Features.FinancialKpi.EventHandlers;

/// <summary>ADR-014, edge 35: WF-14 writes the intake's declared budget and opening spend (financial-kpi.md F-13).</summary>
internal sealed class FinancialKpiIntakeHandler(FinancialOpeningPosition openingPosition) : IProjectIntakeHandler
{
    /// <summary>After Progress, whose period the opening spend sits in.</summary>
    public int Order => 40;

    public Task HandleAsync(ProjectIntakeRecorded intake, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intake);
        return openingPosition.RecordAsync(intake.Data, cancellationToken);
    }
}
