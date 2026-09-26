namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// ADM-041, SSO/Directory Integration: what this environment is configured with and whether it can reach it. The
/// values themselves live in the secret store and the configuration store (CTL-18) and are never returned; the
/// screen shows which are set and tests the connection with them.
/// </summary>
public interface IIdentityIntegrationService
{
    public IdentityIntegrationStatus GetStatus();

    public Task<IdentityIntegrationTestResult> TestConnectionsAsync(CancellationToken cancellationToken);
}
