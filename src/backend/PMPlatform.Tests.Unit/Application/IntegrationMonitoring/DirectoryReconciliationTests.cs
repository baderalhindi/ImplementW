using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IntegrationMonitoring;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Tests.Unit.Application.IntegrationMonitoring;

/// <summary>
/// The directory reconciliation (TASK-075): it compares every linked person and reports, never applies; a directory that cannot be read ends
/// the attempt as a technical failure with the run PARTIAL or FAILED; the alert's detail names people by id and attributes by name, never a value.
/// </summary>
public sealed class DirectoryReconciliationTests
{
    private static readonly IntegrationOperationContext Context = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "key", 1);

    [Fact]
    public async Task EveryPersonIsComparedAndEachMismatchReportedWithoutItsValue()
    {
        Guid agrees = new("00000000-0000-0000-0000-000000000001"), differs = new("00000000-0000-0000-0000-000000000002"), unknown = new("00000000-0000-0000-0000-000000000003");
        FakeSource source = new([Person(agrees, "Engineer"), Person(differs, "Stale title"), Person(unknown, "Engineer")])
        {
            Readings = { [agrees.ToString()] = Found("Engineer"), [differs.ToString()] = Found("Engineer"), [unknown.ToString()] = DirectoryReading.Of(DirectoryReadingOutcome.NotFound) },
        };

        IntegrationOperationResult result = await new DirectoryReconciliation(source).ExecuteAsync(Context, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(new IntegrationRunOutcome(SyncRunStatus.Succeeded, 3, 0, 0, 2, result.Run!.MismatchDetail), result.Run);
        Assert.Equal(
            $"2 of 3 people differ from the directory (job_title 1, department 0, manager 0, not_in_directory 1): {differs}: job_title; {unknown}: not_in_directory",
            result.Run.MismatchDetail);
        Assert.DoesNotContain("Stale title", result.Run.MismatchDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADirectoryThatStopsAnsweringEndsTheAttemptAsATechnicalFailure()
    {
        Guid first = Guid.NewGuid(), second = Guid.NewGuid();
        FakeSource source = new([Person(first, "Engineer"), Person(second, "Engineer")])
        {
            Readings = { [first.ToString()] = Found("Engineer"), [second.ToString()] = DirectoryReading.Of(DirectoryReadingOutcome.Unavailable) },
        };

        IntegrationOperationResult result = await new DirectoryReconciliation(source).ExecuteAsync(Context, CancellationToken.None);

        Assert.Equal((InvocationFailureCategory.Technical, "UNREACHABLE"), (result.FailureCategory, result.ResponseCode));
        Assert.Equal(new IntegrationRunOutcome(SyncRunStatus.Partial, 1, 0, 1, 0, null), result.Run);
    }

    [Fact]
    public void AnAlertNamesTwentyPeopleAndCountsTheRest()
    {
        List<(Guid, IReadOnlyList<string>)> mismatches = [.. Enumerable.Range(1, 25).Select(n => (Guid.NewGuid(), (IReadOnlyList<string>)[DirectoryComparison.Manager]))];

        string detail = DirectoryReconciliation.Detail(mismatches, 30)!;

        Assert.StartsWith("25 of 30 people differ from the directory (job_title 0, department 0, manager 25, not_in_directory 0): ", detail, StringComparison.Ordinal);
        Assert.Equal(20, detail.Split("manager", StringSplitOptions.None).Length - 2);
        Assert.EndsWith("; and 5 more", detail, StringComparison.Ordinal);
    }

    private static DirectoryLinkedPerson Person(Guid id, string jobTitle) => new(id, id.ToString(), jobTitle, "DEPT", null);

    private static DirectoryReading Found(string jobTitle) => new(DirectoryReadingOutcome.Found, jobTitle, "DEPT", null);

    private sealed class FakeSource(IReadOnlyList<DirectoryLinkedPerson> people) : IDirectoryReconciliationSource
    {
        public Dictionary<string, DirectoryReading> Readings { get; } = [];

        public bool IsConfigured => true;

        public Task<IReadOnlyList<DirectoryLinkedPerson>> ListLinkedPeopleAsync(CancellationToken cancellationToken) => Task.FromResult(people);

        public Task<DirectoryReading> ReadAsync(string subjectId, CancellationToken cancellationToken) => Task.FromResult(Readings[subjectId]);
    }
}
