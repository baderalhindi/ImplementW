namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// What a directory reconciliation compares (TASK-075, ADR-007): the platform's copy of the three directory-authoritative attributes of
/// every person the directory is authoritative for, and the directory's values now. Read-only on both sides: a reconciliation reports
/// mismatches and never applies the directory's values, which only a sign-in does (sso-directory-integration.md D-6).
/// </summary>
public interface IDirectoryReconciliationSource
{
    public bool IsConfigured { get; }

    /// <summary>Active internal users linked to the directory by subject, with what the platform holds, ordered by user id.</summary>
    public Task<IReadOnlyList<DirectoryLinkedPerson>> ListLinkedPeopleAsync(CancellationToken cancellationToken);

    /// <summary>What the directory holds for <paramref name="subjectId"/> now.</summary>
    public Task<DirectoryReading> ReadAsync(string subjectId, CancellationToken cancellationToken);
}

/// <summary>
/// A person the directory is authoritative for, as the platform holds them: the department as its directory reference and the manager
/// as their directory subject, so both compare with the directory's own values without resolving anything.
/// </summary>
public sealed record DirectoryLinkedPerson(Guid UserId, string SubjectId, string? JobTitle, string? DepartmentReference, string? ManagerSubjectId);

/// <summary>The directory's values for one subject, or why there are none.</summary>
public sealed record DirectoryReading(DirectoryReadingOutcome Outcome, string? JobTitle, string? DepartmentReference, string? ManagerSubjectId)
{
    public static DirectoryReading Of(DirectoryReadingOutcome outcome) => new(outcome, null, null, null);
}

public enum DirectoryReadingOutcome
{
    /// <summary>The directory answered with the person's entry.</summary>
    Found = 1,

    /// <summary>The directory answered: no entry has that subject.</summary>
    NotFound = 2,

    /// <summary>The directory could not be reached or answered with an error.</summary>
    Unavailable = 3,

    /// <summary>This environment has no directory configured.</summary>
    NotConfigured = 4,
}
