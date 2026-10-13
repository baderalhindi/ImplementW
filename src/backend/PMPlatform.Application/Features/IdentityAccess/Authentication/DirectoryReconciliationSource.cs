using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// The platform's copy of the directory-authoritative attributes, and the directory's, for FG-05's reconciliation (TASK-075). Reads only:
/// nothing here applies a directory value to a user. Each directory read is FG-05 telemetry like any other (the observed adapter).
/// </summary>
internal sealed class DirectoryReconciliationSource(IUserAccessRepository users, IDirectoryService directory) : IDirectoryReconciliationSource
{
    public bool IsConfigured => directory.IsConfigured;

    public Task<IReadOnlyList<DirectoryLinkedPerson>> ListLinkedPeopleAsync(CancellationToken cancellationToken) =>
        users.ListDirectoryLinkedPeopleAsync(cancellationToken);

    public async Task<DirectoryReading> ReadAsync(string subjectId, CancellationToken cancellationToken)
    {
        DirectoryResult result = await directory.FindBySubjectAsync(subjectId, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            { Entry: { } entry } => new DirectoryReading(DirectoryReadingOutcome.Found, entry.JobTitle, entry.DepartmentReference, entry.ManagerSubjectId),
            { Failure: AuthenticationFailure.Rejected } => DirectoryReading.Of(DirectoryReadingOutcome.NotFound),
            { Failure: AuthenticationFailure.NotConfigured } => DirectoryReading.Of(DirectoryReadingOutcome.NotConfigured),
            _ => DirectoryReading.Of(DirectoryReadingOutcome.Unavailable),
        };
    }
}
