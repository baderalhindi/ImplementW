using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// Single sign-on against AHDA's identity provider (TASK-028, ADR-007): the OpenID Connect authorization code flow
/// with PKCE, the platform as a confidential client (<see cref="OpenIdConnectRelyingParty"/>). Only the ID token's
/// subject, and its <c>amr</c> and <c>auth_time</c> for TASK-029, are used. No access token is kept, and no group or role
/// claim is read: platform roles are assigned in the platform.
/// </summary>
internal sealed class OpenIdConnectProvider : ISingleSignOnProvider
{
    public const string HttpClientName = "PMPlatform.Identity.Sso";

    private readonly IConfiguration _configuration;
    private readonly SingleSignOnOptions _options;
    private readonly OpenIdConnectRelyingParty _relyingParty;

    public OpenIdConnectProvider(
        IConfiguration configuration,
        IOptions<SingleSignOnOptions> options,
        IHttpClientFactory httpClients,
        TimeProvider timeProvider,
        ILogger<OpenIdConnectProvider> logger)
    {
        _configuration = configuration;
        _options = options.Value;
        _relyingParty = new OpenIdConnectRelyingParty(
            new RelyingPartySettings(
                HttpClientName, "Identity provider", SingleSignOnClient.AuthorityKey, _options.Scopes, _options.RequireHttpsMetadata, _options.TransactionLifetime),
            httpClients,
            new AuthorizationTransactionProtector(configuration, AuthorizationTransactionProtector.SingleSignOnLabel),
            timeProvider,
            logger);
    }

    public bool IsConfigured => SingleSignOnClient.Read(_configuration) is not null;

    public async Task<SsoAuthorizationResult> BeginAsync(CancellationToken cancellationToken)
    {
        if (SingleSignOnClient.Read(_configuration) is not { } client)
        {
            return new SsoAuthorizationResult(null, null, AuthenticationFailure.NotConfigured);
        }

        (Uri? url, string? transaction, AuthenticationFailure? failure) = await _relyingParty.BeginAsync(client, binding: null, cancellationToken).ConfigureAwait(false);
        return new SsoAuthorizationResult(url, transaction, failure);
    }

    public async Task<SingleSignOnResult> CompleteAsync(string code, string state, string transaction, CancellationToken cancellationToken)
    {
        if (SingleSignOnClient.Read(_configuration) is not { } client)
        {
            return SingleSignOnResult.Failed(AuthenticationFailure.NotConfigured);
        }

        (ValidatedIdToken? idToken, AuthenticationFailure? failure) = await _relyingParty
            .CompleteAsync(client, code, state, transaction, binding: null, cancellationToken)
            .ConfigureAwait(false);
        if (idToken is null)
        {
            return SingleSignOnResult.Failed(failure ?? AuthenticationFailure.Rejected);
        }

        if (!idToken.Claims.TryGetValue(_options.SubjectClaim, out object? subject) || subject is not string { Length: > 0 } subjectId)
        {
            return SingleSignOnResult.Failed(AuthenticationFailure.Rejected);
        }

        // TASK-029: how and when the provider authenticated the person. Whether its amr counts as a second factor is the
        // platform's policy (Identity:Mfa:IdentityProviderMethods), not the provider's say.
        IReadOnlyList<string> methods = [.. idToken.Identity.FindAll("amr").Select(c => c.Value)];
        DateTimeOffset? authenticatedAt = long.TryParse(idToken.Identity.FindFirst("auth_time")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
        return SingleSignOnResult.Authenticated(subjectId, methods, authenticatedAt);
    }

    public SingleSignOnIntegrationStatus Describe() => new(
        IsConfigured,
        OpenIdConnectClient.AbsoluteUri(_configuration[SingleSignOnClient.AuthorityKey]),
        _configuration[SingleSignOnClient.ClientIdKey] is { Length: > 0 } clientId ? clientId : null,
        OpenIdConnectClient.AbsoluteUri(_configuration[SingleSignOnClient.CallbackUrlKey]),
        !string.IsNullOrEmpty(_configuration[ApplicationSecrets.SsoClientSecret]),
        _options.SubjectClaim,
        _options.Scopes);

    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken) =>
        SingleSignOnClient.Read(_configuration) is { } client
            ? _relyingParty.TestConnectionAsync(client, cancellationToken)
            : Task.FromResult(_relyingParty.Result(ConnectionTestOutcome.NotConfigured, failureCode: null));
}
