using PMPlatform.Application.Common.Events;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Project.Contracts.Events;

/// <summary>
/// <c>Project.ProjectIntakeRecorded</c> (event-conventions §4 row 1; ADR-003 §8.2 edge 35): a project already under way was taken in
/// with a Declared Baseline and its opening position (ADR-014). Each owning module writes its own fact from it — Schedule the Declared
/// Baseline, Progress the opening percentage, FinancialKpi the declared budget and opening spend, Milestone the milestones achieved —
/// through <see cref="IProjectIntakeHandler"/> (solution architecture §11.1). The idempotency key is the intake's id.
/// </summary>
public sealed record ProjectIntakeRecorded : EventEnvelope<ProjectIntakeRecordedData>
{
    public const string Type = "Project.ProjectIntakeRecorded";

    public const int CurrentSchemaVersion = 1;
}

/// <summary>The sample's shape (<c>event-samples/project-intake-recorded.json</c>), with the declared scope the Declared Baseline carries.</summary>
public sealed record ProjectIntakeRecordedData(
    Guid ProjectId,
    Guid ProjectIntakeId,
    DateOnly LegacyIntakeDate,
    DeclaredBaselineData DeclaredBaseline,
    OpeningPositionData OpeningPosition,
    Guid RecordedByUserId);

public sealed record DeclaredBaselineData(decimal BudgetSar, DateOnly EndDate, NarrativeText Scope);

public sealed record OpeningPositionData(decimal PercentComplete, decimal SpendToDateSar, IReadOnlyList<AchievedMilestoneData> AchievedMilestones);

public sealed record AchievedMilestoneData(Guid ProjectMilestoneId, DateOnly ActualAchievementDate);
