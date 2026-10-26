namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>The OIDC client settings. Authority, client id and callback URL are Public in the Environment and Secrets sheet; the client secret is reduced to whether it is set.</summary>
public sealed record SingleSignOnIntegrationStatus(
    bool IsConfigured,
    Uri? Authority,
    string? ClientId,
    Uri? CallbackUrl,
    bool ClientSecretConfigured,
    string SubjectClaim,
    IReadOnlyList<string> Scopes);
