using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IntegrationMonitoring;

namespace PMPlatform.Tests.Unit.Application.IntegrationMonitoring;

/// <summary>The directory reconciliation's rule (ADR-007): the three authoritative attributes compared as the directory states them; values never named.</summary>
public sealed class DirectoryComparisonTests
{
    private static readonly DirectoryLinkedPerson Platform = new(Guid.NewGuid(), "subject-1", "Project Manager", "DEPT-1", "manager-subject");

    [Fact]
    public void AgreementIsNoDifference() =>
        Assert.Empty(DirectoryComparison.Differences(Platform, Found("Project Manager", "DEPT-1", "MANAGER-SUBJECT")));

    [Fact]
    public void EachAttributeThatDiffersIsNamedInOrder() =>
        Assert.Equal(
            [DirectoryComparison.JobTitle, DirectoryComparison.Department, DirectoryComparison.Manager],
            DirectoryComparison.Differences(Platform, Found("Programme Manager", "DEPT-2", null)));

    [Fact]
    public void AbsentAndBlankAreTheSame() =>
        Assert.Empty(DirectoryComparison.Differences(Platform with { JobTitle = null }, Found("  ", "DEPT-1", "manager-subject")));

    [Fact]
    public void APersonTheDirectoryDoesNotKnowIsAMismatch() =>
        Assert.Equal([DirectoryComparison.NotInDirectory], DirectoryComparison.Differences(Platform, DirectoryReading.Of(DirectoryReadingOutcome.NotFound)));

    private static DirectoryReading Found(string? jobTitle, string? department, string? manager) => new(DirectoryReadingOutcome.Found, jobTitle, department, manager);
}
