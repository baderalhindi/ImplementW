using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure.Identity.Nafath;

/// <summary>
/// Nafath identity verification over OpenID Connect (TASK-068): the authorization code flow with PKCE, the platform as a
/// confidential client (<see cref="OpenIdConnectRelyingParty"/>), each transaction bound to the user being verified.
/// The verification is that Nafath issued a valid ID token, for this client and this transaction, asserting a national identity
/// that matches the one recorded for the user (client review, item 7; SR-53). Nothing in that token is kept: the identity is passed on
/// only as a keyed fingerprint for that comparison, and the platform records a reference of its own and the time.
/// </summary>
internal sealed partial class NafathIdentityVerifier : IIdentityVerificationProvider
{
    public const string HttpClientName = "PMPlatform.Identity.Nafath";

    private readonly IConfiguration _configuration;
    private readonly NafathOptions _options;
    private readonly IdentityVerificationPolicy _policy;
    private readonly INationalIdProtector _nationalIds;
    private readonly OpenIdConnectRelyingParty _relyingParty;
    private readonly ILogger<NafathIdentityVerifier> _logger;

    public NafathIdentityVerifier(
        IConfiguration configuration,
        IOptions<NafathOptions> options,
        IdentityVerificationPolicy policy,
        INationalIdProtector nationalIds,
        IHttpClientFactory httpClients,
        TimeProvider timeProvider,
        ILogger<NafathIdentityVerifier> logger)
    {
        _configuration = configuration;
        _options = options.Value;
        _policy = policy;
        _nationalIds = nationalIds;
        _logger = logger;
        _relyingParty = new OpenIdConnectRelyingParty(
            new RelyingPartySettings(
                HttpClientName, "Nafath", $"{NafathOptions.Section}:Authority", _options.Scopes, _options.RequireHttpsMetadata, _options.TransactionLifetime),
            httpClients,
            new AuthorizationTransactionProtector(configuration, AuthorizationTransactionProtector.NafathLabel),
            timeProvider,
            logger);
    }

    public bool IsConfigured => Client is not null;

    private OpenIdConnectClient? Client => NafathClient.Read(_configuration, _options);

    public async Task<IdentityVerificationAuthorization> BeginAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (Client is not { } client)
        {
            return IdentityVerificationAuthorization.Failed(AuthenticationFailure.NotConfigured);
        }

        (Uri? url, string? transaction, AuthenticationFailure? failure) = await _relyingParty.BeginAsync(client, Binding(userId), parameters: null, cancellationToken).ConfigureAwait(false);
        return new IdentityVerificationAuthorization(url, transaction, failure);
    }

    public async Task<IdentityVerificationResult> CompleteAsync(Guid userId, string code, string state, string transaction, CancellationToken cancellationToken)
    {
        if (Client is not { } client || !_nationalIds.IsConfigured)
        {
            return IdentityVerificationResult.Failed(AuthenticationFailure.NotConfigured);
        }

        (ValidatedIdToken? idToken, AuthenticationFailure? failure) = await _relyingParty
            .CompleteAsync(client, code, state, transaction, Binding(userId), cancellationToken)
            .ConfigureAwait(false);
        if (idToken is null)
        {
            return IdentityVerificationResult.Failed(failure ?? AuthenticationFailure.Rejected);
        }

        // An ID token that identifies no one verifies no one. The national identity is fingerprinted for the comparison and then dropped.
        if (!idToken.Claims.TryGetValue(_options.NationalIdClaim, out object? asserted) || asserted is not string { Length: > 0 } nationalId)
        {
            LogNoSubject(_logger, userId);
            return IdentityVerificationResult.Failed(AuthenticationFailure.Rejected);
        }

        string reference = Guid.NewGuid().ToString();
        LogVerified(_logger, userId, reference);
        return IdentityVerificationResult.Verified(reference, _nationalIds.Fingerprint(nationalId.Trim()));
    }

    public IdentityVerificationIntegrationStatus Describe() => new(
        _policy.Enabled,
        IsConfigured,
        OpenIdConnectClient.AbsoluteUri(_options.Authority),
        !string.IsNullOrWhiteSpace(_configuration[ApplicationSecrets.NafathClientId]),
        !string.IsNullOrEmpty(_configuration[ApplicationSecrets.NafathClientSecret]),
        OpenIdConnectClient.AbsoluteUri(_configuration[NafathClient.CallbackUrlKey]),
        _configuration[NafathClient.ApplicationIdKey] is { Length: > 0 } applicationId ? applicationId : null,
        _options.Scopes);

    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken) =>
        Client is { } client
            ? _relyingParty.TestConnectionAsync(client, cancellationToken)
            : Task.FromResult(_relyingParty.Result(ConnectionTestOutcome.NotConfigured, failureCode: null));

    /// <summary>The transaction's binding: the platform user the verification is for.</summary>
    private static string Binding(Guid userId) => userId.ToString();

    [LoggerMessage(Level = LogLevel.Information, Message = "Nafath verified user {UserId}; verification reference {Reference}.")]
    private static partial void LogVerified(ILogger logger, Guid userId, string reference);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nafath ID token for user {UserId} asserts no subject; not a verification.")]
    private static partial void LogNoSubject(ILogger logger, Guid userId);
}
