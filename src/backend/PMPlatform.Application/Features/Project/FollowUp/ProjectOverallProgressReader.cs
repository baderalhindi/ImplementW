using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project.FollowUp;

/// <summary>
/// Each project's one overall figure (review round 8, point 02), read for many projects at once so a register page or a dashboard costs a few
/// queries, not a few per project. See <see cref="ProjectOverallProgress"/> for what each method gives.
/// <para>
/// The stage method's % complete per stage comes from the newest of: the latest published stage-based update (when the project's figure is that
/// update's); an accepted entity update, which records the current stage and its %; the stage plan's starting %; the imported current stage. With a
/// current stage and its %, the stages before it are complete and those after it not started. Planned % is today's, from the plan's dates.
/// </para>
/// </summary>
internal sealed class ProjectOverallProgressReader(
    IProjectRepository repository, IConfigurationResolver resolver, IProjectProgressSource source, TimeProvider timeProvider)
{
    private const int Places = 2;

    public async Task<Func<ProjectEntity, ProjectOverallProgress>> ForAsync(IReadOnlyCollection<ProjectEntity> projects, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projects);
        if (projects.Count == 0)
        {
            return _ => ProjectOverallProgress.None;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        List<ProjectEntity> byStages = [.. projects.Where(p => p.ProgressMethod == ProjectProgressMethod.Stages)];
        List<Guid> bySchedule = [.. projects.Where(p => p.ProgressMethod == ProjectProgressMethod.Schedule).Select(p => p.Id)];

        IReadOnlySet<Guid> approved = await source.WithApprovedUpdateAsync([.. projects.Select(p => p.Id)], cancellationToken).ConfigureAwait(false);
        ProjectStageSet? stages = byStages.Count == 0 ? null : await resolver.ResolveStagesAsync(now, cancellationToken).ConfigureAwait(false);
        ILookup<Guid, ProjectStagePlanEntry> plans = byStages.Count == 0
            ? Array.Empty<ProjectStagePlanEntry>().ToLookup(e => e.ProjectId)
            : await repository.ReadStagePlansAsync([.. byStages.Select(p => p.Id)], cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, PublishedStageProgress> published = await source.LatestPublishedStagesAsync(
            [.. byStages.Where(p => p.DeclaredSource == DeclaredValueSource.ProgressUpdate).Select(p => p.Id)], cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, ScheduleProgressFigure> schedules = bySchedule.Count == 0
            ? new Dictionary<Guid, ScheduleProgressFigure>()
            : await source.ScheduleProgressAsync(bySchedule, today, cancellationToken).ConfigureAwait(false);

        return project =>
        {
            bool locked = approved.Contains(project.Id) || project.DeclaredSource == DeclaredValueSource.EntityUpdate;
            return project.ProgressMethod switch
            {
                null => Declared(project, locked),
                ProjectProgressMethod.Schedule => Scheduled(project, schedules.GetValueOrDefault(project.Id), today, locked),
                ProjectProgressMethod.Stages => Staged(project, stages, [.. plans[project.Id]], published.GetValueOrDefault(project.Id), today, locked),
                _ => throw new InvalidOperationException("Unknown progress method."),
            };
        };
    }

    /// <summary>The arithmetic of the stage method alone, for a test of the client's worked example.</summary>
    internal static decimal? StageOverall(IEnumerable<(decimal Weight, bool Applies, decimal? Percent)> stages) =>
        Round(StageProgress.Overall(stages.Where(s => s.Applies).Select(s => (s.Weight, s.Percent))));

    private static ProjectOverallProgress Declared(ProjectEntity project, bool locked) =>
        new(null, project.DeclaredActualPercent is null && project.DeclaredPlannedPercent is null ? null : ProgressFigureSource.Declared,
            project.DeclaredActualPercent, project.DeclaredPlannedPercent, project.DeclaredAsOf, project.CurrentStageCode, [], NeedsSetup: true, locked);

    private static ProjectOverallProgress Scheduled(ProjectEntity project, ScheduleProgressFigure? figure, DateOnly today, bool locked) =>
        new(ProjectProgressMethod.Schedule, ProgressFigureSource.Schedule, Round(figure?.ActualPercent), Round(figure?.PlannedPercent),
            figure is null ? null : today, project.CurrentStageCode, [], NeedsSetup: false, locked);

    private static ProjectOverallProgress Staged(
        ProjectEntity project, ProjectStageSet? stages, IReadOnlyList<ProjectStagePlanEntry> plan, PublishedStageProgress? published, DateOnly today, bool locked)
    {
        bool hasPlan = project.StagePlanRevisionNo > 0 && plan.Count > 0;
        if (stages is null)
        {
            // No PROJECT_STAGE version is in force: there is nothing to weigh, so the declared figure stands, labelled as declared.
            return Declared(project, locked) with { Method = ProjectProgressMethod.Stages, NeedsSetup = false };
        }

        (Func<string, decimal?> completion, Func<string, (decimal Weight, bool Applies)?> recorded, DateOnly? asOf) = CompletionOf(project, stages, plan, published);
        List<ProjectStageFigure> figures = [.. stages.Stages.Select(stage =>
        {
            ProjectStagePlanEntry? entry = plan.FirstOrDefault(e => e.StageCode == stage.Code);
            (decimal weight, bool applies) = recorded(stage.Code) ?? (stage.WeightPercent, entry?.Applies ?? true);
            decimal? planned = applies && entry is { PlannedStartDate: { } start, PlannedEndDate: { } end } ? StageProgress.PlannedPercent(start, end, today) : null;
            return new ProjectStageFigure(stage.Code, stage.Name, weight, applies, Round(planned), Round(applies ? completion(stage.Code) : null));
        })];
        List<ProjectStageFigure> applying = [.. figures.Where(f => f.Applies)];
        decimal? actual = Round(StageProgress.Overall(applying.Select(f => (f.WeightPercent, f.PercentComplete))));
        decimal? plannedOverall = hasPlan ? Round(StageProgress.Overall(applying.Select(f => (f.WeightPercent, f.PlannedPercent)))) : null;
        return new(ProjectProgressMethod.Stages, ProgressFigureSource.Stages, actual, plannedOverall, actual is null ? null : asOf, project.CurrentStageCode, figures, !hasPlan, locked);
    }

    /// <summary>Each stage's % complete, the weights a published update recorded, and the date the figures hold for.</summary>
    private static (Func<string, decimal?> Completion, Func<string, (decimal, bool)?> Recorded, DateOnly? AsOf) CompletionOf(
        ProjectEntity project, ProjectStageSet stages, IReadOnlyList<ProjectStagePlanEntry> plan, PublishedStageProgress? published)
    {
        if (published is not null)
        {
            Dictionary<string, PublishedStageFigure> byCode = published.Stages.ToDictionary(s => s.StageCode, StringComparer.Ordinal);
            return (code => byCode.GetValueOrDefault(code)?.PercentComplete,
                code => byCode.TryGetValue(code, out PublishedStageFigure? s) ? (s.WeightPercent, s.Applies) : null,
                published.PublishedOn);
        }

        if (project.DeclaredSource != DeclaredValueSource.EntityUpdate && plan.Any(e => e.StartingPercent is not null))
        {
            // The starting % holds from the day the plan was last saved.
            return (code => plan.FirstOrDefault(e => e.StageCode == code) is { } e ? e.StartingPercent ?? 0m : 0m, _ => null,
                DateOnly.FromDateTime(plan.Max(e => e.UpdatedAt).UtcDateTime));
        }

        if (project.CurrentStageCode is { } current && stages.Stages.ToList().FindIndex(s => s.Code == current) is var index and >= 0)
        {
            List<string> order = [.. stages.Stages.Select(s => s.Code)];
            return (code => order.IndexOf(code) is var at && at < index ? 100m : at == index ? project.CurrentStagePercent ?? 0m : 0m, _ => null, project.DeclaredAsOf);
        }

        return (_ => null, _ => null, null);
    }

    private static decimal? Round(decimal? percent) => percent is { } value ? decimal.Round(value, Places, MidpointRounding.AwayFromZero) : null;
}
