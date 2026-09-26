using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>ADM-041: the directory and SSO settings in effect, and a test of each connection.</summary>
internal sealed class IdentityIntegrationService(IDirectoryService directory, ISingleSignOnProvider singleSignOn) : IIdentityIntegrationService
{
    public IdentityIntegrationStatus GetStatus() => new(directory.Describe(), singleSignOn.Describe());

    public async Task<IdentityIntegrationTestResult> TestConnectionsAsync(CancellationToken cancellationToken)
    {
        Task<ConnectionTestResult> directoryTest = directory.TestConnectionAsync(cancellationToken);
        Task<ConnectionTestResult> singleSignOnTest = singleSignOn.TestConnectionAsync(cancellationToken);
        return new IdentityIntegrationTestResult(
            await directoryTest.ConfigureAwait(false),
            await singleSignOnTest.ConfigureAwait(false));
    }
}
