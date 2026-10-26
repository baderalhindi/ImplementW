namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// The Nafath client settings (TASK-068). The Environment and Secrets sheet classifies <c>NAFATH_CLIENT_ID</c> and
/// <c>NAFATH_CLIENT_SECRET</c> as Secret, so both are reduced to whether they are set; the callback URL and application
/// id are Public. <see cref="IsEnabled"/> is the feature flag, <see cref="IsConfigured"/> whether the client is complete.
/// </summary>
public sealed record IdentityVerificationIntegrationStatus(
    bool IsEnabled,
    bool IsConfigured,
    Uri? Authority,
    bool ClientIdConfigured,
    bool ClientSecretConfigured,
    Uri? CallbackUrl,
    string? ApplicationId,
    IReadOnlyList<string> Scopes);
