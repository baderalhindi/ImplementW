using PMPlatform.Application.Common.Graphs;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// The backend's authoritative date calculation (the spec's §11.3, §12.3), run over the whole working schedule after every
/// change to it. A node with children is a summary; any other is a leaf. A leaf's planned start is the later of its
/// requested start and every dependency constraint — FS: the working day after the predecessor's finish, SS: the
/// predecessor's start, FF: no finish before the predecessor's finish, each moved by the lag — and its finish follows from
/// its duration. A summary spans its children's dates. Until the project has an ACTIVE baseline a leaf's forecast follows
/// its plan; after, the forecast is the planner's, and only summaries' forecasts are rolled up. Cancelled nodes take no part.
/// </summary>
internal static class ScheduleCalculation
{
    /// <summary>Recalculates <paramref name="activities"/> in place and returns those whose calculated values changed.</summary>
    public static IReadOnlyList<ScheduleActivity> Recalculate(
        IReadOnlyList<ScheduleActivity> activities, IReadOnlyList<ScheduleDependency> dependencies, Func<ScheduleActivity, bool> forecastFollowsPlan)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(forecastFollowsPlan);

        Dictionary<Guid, Calculated> before = activities.ToDictionary(a => a.Id, Calculated.Of);
        ILookup<Guid, ScheduleActivity> children = activities.Where(a => a.ParentActivityId is not null).ToLookup(a => a.ParentActivityId!.Value);
        foreach (ScheduleActivity activity in activities)
        {
            activity.ActivityKind = children[activity.Id].Any() ? ScheduleActivityKind.Summary : ScheduleActivityKind.Activity;
        }

        PlanLeaves(activities, dependencies, forecastFollowsPlan);
        Dictionary<Guid, ScheduleActivity> byId = activities.ToDictionary(a => a.Id);
        foreach (ScheduleActivity root in activities.Where(a => a.ParentActivityId is null || !byId.ContainsKey(a.ParentActivityId.Value)))
        {
            RollUp(root, children);
        }

        return [.. activities.Where(a => Calculated.Of(a) != before[a.Id])];
    }

    /// <summary>The project's forecast finish: the latest forecast finish of a live leaf; null without one.</summary>
    public static DateOnly? ForecastFinish(IEnumerable<ScheduleActivity> activities) =>
        activities.Where(IsLiveLeaf).Max(a => (DateOnly?)a.ForecastFinishDate);

    /// <summary>The project's planned finish: the latest planned finish of a live leaf; null without one.</summary>
    public static DateOnly? PlannedFinish(IEnumerable<ScheduleActivity> activities) =>
        activities.Where(IsLiveLeaf).Max(a => (DateOnly?)a.PlannedFinishDate);

    public static bool IsLiveLeaf(ScheduleActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return activity.ActivityKind == ScheduleActivityKind.Activity && activity.Status != ScheduleActivityStatus.Cancelled;
    }

    private static void PlanLeaves(IReadOnlyList<ScheduleActivity> activities, IReadOnlyList<ScheduleDependency> dependencies, Func<ScheduleActivity, bool> forecastFollowsPlan)
    {
        Dictionary<Guid, ScheduleActivity> leaves = activities.Where(IsLiveLeaf).ToDictionary(a => a.Id);
        List<ScheduleDependency> live = [.. dependencies.Where(d => leaves.ContainsKey(d.PredecessorActivityId) && leaves.ContainsKey(d.SuccessorActivityId))];
        ILookup<Guid, ScheduleDependency> into = live.ToLookup(d => d.SuccessorActivityId);
        IReadOnlyList<Guid> order = DependencyGraph.TopologicalOrder(
            [.. leaves.Values.OrderBy(a => a.SortOrder).ThenBy(a => a.WbsCode, StringComparer.Ordinal).Select(a => a.Id)],
            live.Select(d => (d.PredecessorActivityId, d.SuccessorActivityId)));

        foreach (Guid id in order)
        {
            ScheduleActivity leaf = leaves[id];
            DateOnly start = leaf.RequestedStartDate;
            foreach (ScheduleDependency dependency in into[id])
            {
                ScheduleActivity predecessor = leaves[dependency.PredecessorActivityId];
                DateOnly earliest = dependency.DependencyType switch
                {
                    ScheduleDependencyType.Fs => WorkingDays.Add(predecessor.PlannedFinishDate, 1 + dependency.LagDays),
                    ScheduleDependencyType.Ss => WorkingDays.Add(predecessor.PlannedStartDate, dependency.LagDays),
                    ScheduleDependencyType.Ff => WorkingDays.StartOf(WorkingDays.Add(predecessor.PlannedFinishDate, dependency.LagDays), leaf.PlannedDurationDays),
                    _ => throw new ArgumentOutOfRangeException(nameof(dependencies), dependency.DependencyType, "Unknown dependency type."),
                };
                start = earliest > start ? earliest : start;
            }

            leaf.PlannedStartDate = start;
            leaf.PlannedFinishDate = WorkingDays.FinishOf(start, leaf.PlannedDurationDays);
            if (forecastFollowsPlan(leaf))
            {
                leaf.ForecastStartDate = leaf.PlannedStartDate;
                leaf.ForecastFinishDate = leaf.PlannedFinishDate;
            }
        }
    }

    /// <summary>A summary spans its live children, bottom up; one whose children are all cancelled keeps its last dates.</summary>
    private static void RollUp(ScheduleActivity node, ILookup<Guid, ScheduleActivity> children)
    {
        if (node.ActivityKind != ScheduleActivityKind.Summary)
        {
            return;
        }

        List<ScheduleActivity> live = [];
        foreach (ScheduleActivity child in children[node.Id])
        {
            RollUp(child, children);
            if (child.Status != ScheduleActivityStatus.Cancelled)
            {
                live.Add(child);
            }
        }

        if (live.Count == 0)
        {
            return;
        }

        node.PlannedStartDate = live.Min(c => c.PlannedStartDate);
        node.PlannedFinishDate = live.Max(c => c.PlannedFinishDate);
        node.PlannedDurationDays = WorkingDays.Span(node.PlannedStartDate, node.PlannedFinishDate);
        node.ForecastStartDate = live.Min(c => c.ForecastStartDate);
        node.ForecastFinishDate = live.Max(c => c.ForecastFinishDate);
    }

    private sealed record Calculated(ScheduleActivityKind Kind, DateOnly PlannedStart, DateOnly PlannedFinish, int Duration, DateOnly ForecastStart, DateOnly ForecastFinish)
    {
        public static Calculated Of(ScheduleActivity a) =>
            new(a.ActivityKind, a.PlannedStartDate, a.PlannedFinishDate, a.PlannedDurationDays, a.ForecastStartDate, a.ForecastFinishDate);
    }
}
