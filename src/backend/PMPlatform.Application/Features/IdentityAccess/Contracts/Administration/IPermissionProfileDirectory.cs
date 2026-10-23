namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// Permission profiles as a person names them (E-U1): a migration workbook binds an assignment to a profile by its code, such as
/// <c>R04-DEFAULT</c> (TASK-102). An assignment binds the version in force (ADR-018).
/// </summary>
public interface IPermissionProfileDirectory
{
    /// <summary>The profile's latest PUBLISHED version, if it has one.</summary>
    public Task<Guid?> FindPublishedVersionIdAsync(string profileCode, CancellationToken cancellationToken);

    /// <summary>Every profile with a PUBLISHED version, by code.</summary>
    public Task<IReadOnlyList<PermissionProfileSummary>> ListAssignableAsync(CancellationToken cancellationToken);
}
