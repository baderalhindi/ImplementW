namespace PMPlatform.Tests.Unit.Architecture;

/// <summary>
/// ICD-04's split authority, checked on every build (TASK-050): one shared milestone, two owners. WF-03 alone holds the milestone
/// row and its baseline copy — its schedule representation, dates and status; WF-05 alone holds the achievement revisions —
/// their evidence and the accepted Actual Achievement Date. Each module reaches the other's side only through the contracts of
/// ADR-003 §8.2 edge 10 (<c>IProjectMilestoneReader</c>, <c>IMilestoneAchievementRecorder</c>), so neither can write the other's
/// facts, and WF-05 has no milestone object of its own to duplicate the shared one.
/// </summary>
public sealed class MilestoneIdentityTests
{
    /// <summary>WF-03's rows: the shared milestone identity and a baseline's copy of its date.</summary>
    private static readonly string[] ScheduleRows = ["PMPlatform.Domain.Schedule.ProjectMilestone", "PMPlatform.Domain.Schedule.BaselineMilestone"];

    /// <summary>WF-05's row: an achievement revision.</summary>
    private static readonly string[] MilestoneRows = ["PMPlatform.Domain.Milestone.MilestoneAchievement"];

    private static readonly string[] ScheduleOwners =
    [
        "PMPlatform.Application.Features.Schedule",
        "PMPlatform.Domain.Schedule",
        "PMPlatform.Infrastructure.Persistence.Schedule",
        "PMPlatform.Infrastructure.Persistence.Configurations.Schedule",
    ];

    /// <summary>
    /// The foreign key <c>milestone_achievement.project_milestone_id</c> → <c>schedule.project_milestone</c> (ERD; migration
    /// TASK-050 4 of 6): the way WF-05 names the shared row, by identity, instead of holding a copy of it.
    /// </summary>
    private const string SharedRowForeignKey = "PMPlatform.Infrastructure.Persistence.Configurations.Milestone.MilestoneAchievementConfiguration";

    private static readonly string[] MilestoneOwners =
    [
        "PMPlatform.Application.Features.Milestone",
        "PMPlatform.Domain.Milestone",
        "PMPlatform.Infrastructure.Persistence.Milestone",
        "PMPlatform.Infrastructure.Persistence.Configurations.Milestone",
    ];

    [Fact]
    public void OnlyScheduleReachesTheSharedMilestoneRow() =>
        Assert.Equal([$"{SharedRowForeignKey} -> PMPlatform.Domain.Schedule.ProjectMilestone"], ReferencesOutside(ScheduleOwners, ScheduleRows).Distinct());

    [Fact]
    public void OnlyMilestoneReachesTheAchievementRevisions() =>
        Assert.Empty(ReferencesOutside(MilestoneOwners, MilestoneRows));

    /// <summary>Never a duplicated milestone object: WF-05's domain is the revision and nothing else.</summary>
    [Fact]
    public void Wf05HoldsNoMilestoneOfItsOwn() =>
        Assert.Equal(
            ["PMPlatform.Domain.Milestone.MilestoneAchievement", "PMPlatform.Domain.Milestone.MilestoneAchievementStatus"],
            Solution.AllTypes().Where(t => Solution.NamespaceOf(t) == "PMPlatform.Domain.Milestone").Select(t => t.FullName).Order(StringComparer.Ordinal));

    /// <summary>The checks above look for real types: a rename must not make them pass vacuously.</summary>
    [Fact]
    public void TheGuardedTypesExist()
    {
        HashSet<string> types = [.. Solution.AllTypes().Select(t => t.FullName)];
        Assert.All(ScheduleRows.Concat(MilestoneRows), name => Assert.Contains(name, types));
    }

    private static IEnumerable<string> ReferencesOutside(string[] owners, string[] guarded) =>
        Solution.AllTypes()
            .Where(t => !owners.Any(owner => IsIn(Solution.NamespaceOf(t), owner)))
            .SelectMany(t => Solution.ReferencedTypes(t).Select(r => (Type: t, Referenced: r)))
            .Where(x => guarded.Contains(Solution.Outermost(x.Referenced).FullName))
            .Select(x => $"{x.Type.FullName} -> {x.Referenced.FullName}");

    private static bool IsIn(string ns, string owner) =>
        ns == owner || ns.StartsWith($"{owner}.", StringComparison.Ordinal);
}
