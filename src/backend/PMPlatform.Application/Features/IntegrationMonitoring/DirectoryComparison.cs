using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// The directory reconciliation's rule (TASK-075, ADR-007): the three attributes the directory is authoritative for, compared as the
/// directory states them with what the platform holds. Absent and blank are the same; a subject is compared without regard to case. The
/// rule names what differs and never the values, which are personal data.
/// </summary>
public static class DirectoryComparison
{
    public const string JobTitle = "job_title";
    public const string Department = "department";
    public const string Manager = "manager";
    public const string NotInDirectory = "not_in_directory";

    /// <summary>The attributes that differ, in a fixed order; empty when the platform agrees with the directory.</summary>
    public static IReadOnlyList<string> Differences(DirectoryLinkedPerson platform, DirectoryReading directory)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(directory);
        if (directory.Outcome == DirectoryReadingOutcome.NotFound)
        {
            return [NotInDirectory];
        }

        List<string> differences = [];
        if (!Same(platform.JobTitle, directory.JobTitle, StringComparison.Ordinal))
        {
            differences.Add(JobTitle);
        }

        if (!Same(platform.DepartmentReference, directory.DepartmentReference, StringComparison.Ordinal))
        {
            differences.Add(Department);
        }

        if (!Same(platform.ManagerSubjectId, directory.ManagerSubjectId, StringComparison.OrdinalIgnoreCase))
        {
            differences.Add(Manager);
        }

        return differences;
    }

    private static bool Same(string? platform, string? directory, StringComparison comparison) =>
        string.Equals(Normalized(platform), Normalized(directory), comparison);

    private static string? Normalized(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
