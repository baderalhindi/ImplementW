namespace PMPlatform.Tests.Unit.Architecture;

/// <summary>
/// TASK-075, checked on every build: integration health stays apart from business lifecycle state, in both directions (Blueprint §17.1,
/// BR-INT-001/-002). No module outside FG-05 can read an integration's health or alerts, so none can let it colour a business state; and
/// FG-05 reaches other modules only through the read contracts listed here — the directory reconciliation's, the telemetry events and the
/// paging and result types — so it has no command through which a failed invocation could change a source record.
/// </summary>
public sealed class IntegrationHealthSeparationTests
{
    private const string Feature = "PMPlatform.Application.Features.IntegrationMonitoring";
    private const string Domain = "PMPlatform.Domain.IntegrationMonitoring";

    /// <summary>FG-05 itself, its persistence and its API; and the audit forwarding record's foreign key to an invocation (TASK-033 F-3).</summary>
    private static readonly string[] Owners =
    [
        Feature,
        Domain,
        "PMPlatform.Infrastructure.Persistence.IntegrationMonitoring",
        "PMPlatform.Infrastructure.Persistence.Configurations.IntegrationMonitoring",
        "PMPlatform.Infrastructure.Persistence.Configurations.AuditActivity.AuditForwardingRecordConfiguration",
        "PMPlatform.Infrastructure.IntegrationMonitoring",
        "PMPlatform.Api.Models.IntegrationMonitoring",
        "PMPlatform.Api.Controllers.IntegrationsController",
        "PMPlatform.Api.Controllers.IntegrationInvocationsController",
        "PMPlatform.Api.Controllers.SyncRunsController",
        "PMPlatform.Api.Controllers.IntegrationReconciliationsController",
        "PMPlatform.Api.Controllers.OperationalAlertsController",
    ];

    /// <summary>What FG-05 may use of other modules: reads, events and shared result shapes; no service that writes.</summary>
    private static readonly string[] ForeignContracts =
    [
        "PMPlatform.Application.Features.IdentityAccess.Contracts.IDirectoryReconciliationSource",
        "PMPlatform.Application.Features.IdentityAccess.Contracts.DirectoryLinkedPerson",
        "PMPlatform.Application.Features.IdentityAccess.Contracts.DirectoryReading",
        "PMPlatform.Application.Features.IdentityAccess.Contracts.DirectoryReadingOutcome",
        "PMPlatform.Application.Features.IdentityAccess.Contracts.Events.IntegrationInvocationCompleted",
        "PMPlatform.Application.Features.Notifications.Contracts.Events.IntegrationInvocationCompleted",
        "PMPlatform.Application.Features.IdentityAccess.Contracts.Administration.AdministrationResult`1",
        "PMPlatform.Application.Features.IdentityAccess.Contracts.Administration.AdministrationError",
        "PMPlatform.Application.Features.IdentityAccess.Contracts.Administration.FieldIssue",
        "PMPlatform.Application.Features.IdentityAccess.Contracts.Administration.PageRequest",
        "PMPlatform.Application.Features.IdentityAccess.Contracts.Administration.Versioned`1",
    ];

    [Fact]
    public void NoModuleOutsideFg05ReachesIntegrationHealth() =>
        Assert.Empty(Solution.AllTypes()
            .Where(t => !Owners.Any(owner => IsIn(t.FullName, owner) || IsIn(Solution.NamespaceOf(t), owner)))
            .SelectMany(t => Solution.ReferencedTypes(t).Select(r => (Type: t, Referenced: r)))
            .Where(x => IsIn(Solution.NamespaceOf(x.Referenced), Domain))
            .Select(x => $"{x.Type.FullName} -> {x.Referenced.FullName}")
            .Distinct());

    [Fact]
    public void Fg05ReachesOtherModulesOnlyThroughTheirReadContractsAndEvents() =>
        Assert.Empty(Solution.AllTypes()
            .Where(t => IsIn(Solution.NamespaceOf(t), Feature))
            .SelectMany(t => Solution.ReferencedTypes(t).Select(r => (Type: t, Referenced: Solution.Outermost(r))))
            .Where(x => ModuleRegistry.ModuleOf(Solution.NamespaceOf(x.Referenced)) is { } module && module != "IntegrationMonitoring")
            .Where(x => !ForeignContracts.Contains(x.Referenced.FullName))
            .Select(x => $"{x.Type.FullName} -> {x.Referenced.FullName}")
            .Distinct());

    /// <summary>The checks above look for real types: a rename must not make them pass vacuously.</summary>
    [Fact]
    public void TheGuardedTypesExist()
    {
        HashSet<string> types = [.. Solution.AllTypes().Select(t => t.FullName)];
        Assert.All(ForeignContracts, name => Assert.Contains(name, types));
        Assert.Contains($"{Domain}.IntegrationInstance", types);
        Assert.Contains($"{Domain}.OperationalAlert", types);
    }

    private static bool IsIn(string name, string owner) =>
        name == owner || name.StartsWith($"{owner}.", StringComparison.Ordinal) || name.StartsWith($"{owner}/", StringComparison.Ordinal);
}
