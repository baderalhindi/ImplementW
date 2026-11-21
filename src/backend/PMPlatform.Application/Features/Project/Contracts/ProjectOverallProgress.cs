using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>Where a project's overall figure comes from (review round 8, point 02): its chosen method, or the declared figure while it has none.</summary>
public enum ProgressFigureSource
{
    Schedule = 1,
    Stages = 2,
    Declared = 3,
}

/// <summary>One stage of a stage-method figure: its weight, whether it applies, its planned % today and its % complete.</summary>
public sealed record ProjectStageFigure(string StageCode, BilingualLabel Name, decimal WeightPercent, bool Applies, decimal? PlannedPercent, decimal? PercentComplete);

/// <summary>
/// A project's one overall progress figure (review round 8, point 02), the same on the Overview, the register, the dashboards and the cards. The
/// gap and the on-plan label are calculated from <see cref="ActualPercent"/> and <see cref="PlannedPercent"/>.
/// <list type="bullet">
/// <item>Schedule method: the work breakdown's weighted progress and the baseline's planned % today, while the project has an active baseline.</item>
/// <item>Stage method: Σ weight × % complete over the stages that apply, and the same sum over each stage's planned % today from the plan's dates.</item>
/// <item>No method: the declared figure, as imported or entered, labelled as declared.</item>
/// </list>
/// <see cref="NeedsSetup"/> is true while there is no method, or the stage method has no plan. <see cref="MethodChangeNeedsReason"/> is true once
/// a progress update was approved.
/// </summary>
public sealed record ProjectOverallProgress(
    ProjectProgressMethod? Method,
    ProgressFigureSource? Source,
    decimal? ActualPercent,
    decimal? PlannedPercent,
    DateOnly? AsOf,
    string? CurrentStageCode,
    IReadOnlyList<ProjectStageFigure> Stages,
    bool NeedsSetup,
    bool MethodChangeNeedsReason)
{
    public static ProjectOverallProgress None { get; } = new(null, null, null, null, null, null, [], true, false);
}

/// <summary>A stage's % complete in a published period, with the weight that period weighed it by.</summary>
public sealed record PublishedStageFigure(string StageCode, decimal WeightPercent, bool Applies, decimal? PercentComplete);

/// <summary>The latest published stage-based period of a project: when it was published and its stages.</summary>
public sealed record PublishedStageProgress(DateOnly PublishedOn, IReadOnlyList<PublishedStageFigure> Stages);

/// <summary>A schedule's figure today: the work breakdown's weighted actual %, and the active baseline's planned %.</summary>
public sealed record ScheduleProgressFigure(decimal? ActualPercent, decimal? PlannedPercent);

/// <summary>
/// What the overall figure needs from Progress, which owns progress updates and reads the schedule (review round 8, point 02). The Project module
/// declares it and Progress implements it, as with <see cref="IStagePlanChangeAuthorization"/>, so Project never references Progress.
/// </summary>
public interface IProjectProgressSource
{
    /// <summary>For each project that has one, its latest published stage-based period.</summary>
    public Task<IReadOnlyDictionary<Guid, PublishedStageProgress>> LatestPublishedStagesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    /// <summary>For each project with an active baseline, the schedule's figure on <paramref name="asOf"/>.</summary>
    public Task<IReadOnlyDictionary<Guid, ScheduleProgressFigure>> ScheduleProgressAsync(IReadOnlyCollection<Guid> projectIds, DateOnly asOf, CancellationToken cancellationToken);

    /// <summary>The projects that have at least one approved (published) progress update.</summary>
    public Task<IReadOnlySet<Guid>> WithApprovedUpdateAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);
}
