using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>AHDA's identity provider over OpenID Connect (<c>SSO_OIDC_*</c>). Implemented in Infrastructure/Identity.</summary>
public interface ISingleSignOnProvider
{
    public bool IsConfigured { get; }

    public Task<SsoAuthorizationResult> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Redeems the code and validates the ID token (signature, issuer, audience, lifetime, nonce); returns its subject.</summary>
    public Task<SingleSignOnResult> CompleteAsync(string code, string state, string transaction, CancellationToken cancellationToken);

    public SingleSignOnIntegrationStatus Describe();

    /// <summary>Resolves the discovery document and checks its issuer against <c>SSO_OIDC_AUTHORITY</c>.</summary>
    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken);
}
