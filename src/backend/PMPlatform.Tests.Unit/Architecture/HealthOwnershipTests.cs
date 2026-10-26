namespace PMPlatform.Tests.Unit.Architecture;

/// <summary>
/// ICD-03 and M-12, checked on every build: Overall Project Health is computed and stored by WF-02 and nowhere else (TASK-044).
/// A dashboard, report or any other module reads it through <c>Progress.Contracts</c> and never reaches the calculation or
/// the rows, so it can render the published value but cannot recalculate or rewrite it.
/// </summary>
public sealed class HealthOwnershipTests
{
    private const string ProgressFeature = "PMPlatform.Application.Features.Progress";

    /// <summary>The calculation: the health rule and the roll-up it rates.</summary>
    private static readonly string[] Calculation =
    [
        $"{ProgressFeature}.OverallHealthRule",
        $"{ProgressFeature}.HealthThresholds",
        $"{ProgressFeature}.HealthRule",
        $"{ProgressFeature}.ProgressRollup",
    ];

    /// <summary>The stored health: the published snapshot and the live projection.</summary>
    private static readonly string[] HealthRows =
    [
        "PMPlatform.Domain.Progress.PublishedProgressSnapshot",
        "PMPlatform.Domain.Progress.ProjectHealthStatus",
    ];

    /// <summary>The module itself, its entities, and its own persistence.</summary>
    private static readonly string[] Owners =
    [
        ProgressFeature,
        "PMPlatform.Domain.Progress",
        "PMPlatform.Infrastructure.Persistence.Progress",
        "PMPlatform.Infrastructure.Persistence.Configurations.Progress",
    ];

    [Fact]
    public void OnlyProgressReachesTheHealthCalculation() =>
        Assert.Empty(ReferencesOutsideTheOwners(Calculation));

    [Fact]
    public void OnlyProgressReachesTheStoredHealth() =>
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
