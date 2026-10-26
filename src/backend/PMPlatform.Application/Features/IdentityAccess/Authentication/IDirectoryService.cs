using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>AHDA's directory over LDAP (<c>AD_LDAP_URL</c>). Implemented in Infrastructure/Identity.</summary>
public interface IDirectoryService
{
    public bool IsConfigured { get; }

    /// <summary>Finds the person by username with the service account, then binds as them with <paramref name="password"/>.</summary>
    public Task<DirectoryResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken);

    /// <summary>Reads the person's directory-authoritative attributes by subject, e.g. after an SSO sign-in.</summary>
    public Task<DirectoryResult> FindBySubjectAsync(string subjectId, CancellationToken cancellationToken);

    public DirectoryIntegrationStatus Describe();

    /// <summary>Binds as the service account and reads the search base.</summary>
    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken);
}
