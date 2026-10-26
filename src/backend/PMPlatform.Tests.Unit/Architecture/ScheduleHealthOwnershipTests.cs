namespace PMPlatform.Tests.Unit.Architecture;

/// <summary>
/// M-12, checked on every build: Schedule Health and variance are computed and stored by WF-03 and nowhere else (TASK-046).
/// Another module reads the stored value through <c>Schedule.Contracts</c> and never reaches the calculation or the row, so
/// it can render Schedule Health but cannot recalculate or rewrite it.
/// </summary>
public sealed class ScheduleHealthOwnershipTests
{
    private const string ScheduleFeature = "PMPlatform.Application.Features.Schedule";

    /// <summary>The calculation: the health rule, its thresholds and the variance it rates.</summary>
    private static readonly string[] Calculation =
    [
        $"{ScheduleFeature}.ScheduleHealthRule",
        $"{ScheduleFeature}.ScheduleHealthThresholds",
        $"{ScheduleFeature}.ScheduleVariance",
    ];

    /// <summary>The stored health: the live projection.</summary>
    private static readonly string[] HealthRows = ["PMPlatform.Domain.Schedule.ScheduleHealthStatus"];

    /// <summary>The module itself, its entities, and its own persistence.</summary>
    private static readonly string[] Owners =
    [
        ScheduleFeature,
        "PMPlatform.Domain.Schedule",
        "PMPlatform.Infrastructure.Persistence.Schedule",
        "PMPlatform.Infrastructure.Persistence.Configurations.Schedule",
    ];

    [Fact]
    public void OnlyScheduleReachesTheHealthCalculation() =>
        Assert.Empty(ReferencesOutsideTheOwners(Calculation));

    [Fact]
    public void OnlyScheduleReachesTheStoredHealth() =>
        Assert.Empty(ReferencesOutsideTheOwners(HealthRows));

    /// <summary>The checks above look for real types: a rename must not make them pass vacuously.</summary>
    [Fact]
    public void TheGuardedTypesExist()
    {
        HashSet<string> types = [.. Solution.AllTypes().Select(t => t.FullName)];
        Assert.All(Calculation.Concat(HealthRows), name => Assert.Contains(name, types));
    }

    private static IEnumerable<string> ReferencesOutsideTheOwners(string[] guarded) =>
        Solution.AllTypes()
            .Where(t => !Owners.Any(owner => IsIn(Solution.NamespaceOf(t), owner)))
            .SelectMany(t => Solution.ReferencedTypes(t).Select(r => (Type: t, Referenced: r)))
            .Where(x => guarded.Contains(Solution.Outermost(x.Referenced).FullName))
            .Select(x => $"{x.Type.FullName} -> {x.Referenced.FullName}");

    private static bool IsIn(string ns, string owner) =>
        ns == owner || ns.StartsWith($"{owner}.", StringComparison.Ordinal);
}
