using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using System.Text.Json;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// The WebAuthn relying party over Fido2 (passwordless-lib). A ceremony's options travel sealed with the client
/// (<see cref="AuthorizationTransactionProtector"/>), so nothing is stored between the two calls and any replica can finish
/// what another began. Passkeys are discoverable and user-verified, on the device's own platform authenticator (Face ID,
/// Touch ID, Windows Hello); no attestation is asked for, so any such device is accepted and nothing about it is kept.
/// </summary>
internal sealed partial class FidoPasskeyRelyingParty(
    IConfiguration configuration,
    IOptions<PasskeyOptions> options,
    TimeProvider timeProvider,
    ILogger<FidoPasskeyRelyingParty> logger) : IPasskeyRelyingParty
{
    private readonly AuthorizationTransactionProtector _ceremonies = new(configuration, AuthorizationTransactionProtector.PasskeyLabel);

    public bool IsConfigured => Identity is not null;

    private PasskeyRelyingPartyIdentity? Identity => options.Value.Resolve(configuration);

    public PasskeyCeremony BeginRegistration(PasskeyHolder holder, IReadOnlyList<byte[]> registeredCredentialIds)
    {
        ArgumentNullException.ThrowIfNull(holder);
        ArgumentNullException.ThrowIfNull(registeredCredentialIds);
        CredentialCreateOptions created = Fido().RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User { Id = holder.UserId.ToByteArray(), Name = holder.Username, DisplayName = holder.DisplayName },
            ExcludeCredentials = [.. registeredCredentialIds.Select(id => new PublicKeyCredentialDescriptor(id))],
            AuthenticatorSelection = new AuthenticatorSelection
            {
                AuthenticatorAttachment = AuthenticatorAttachment.Platform,
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Required,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });
        string json = created.ToJson();
        return new PasskeyCeremony(json, _ceremonies.Protect(new Ceremony(json, holder.UserId, Expiry())));
    }

    public async Task<NewPasskeyCredential?> CompleteRegistrationAsync(Guid userId, string transaction, string credentialJson, CancellationToken cancellationToken)
    {
        if (Open(transaction) is not { UserId: { } owner } ceremony || owner != userId)
        {
            return null;
        }

        try
        {
            AuthenticatorAttestationRawResponse? response = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(credentialJson);
            if (response is null)
            {
                return null;
            }

            RegisteredPublicKeyCredential credential = await Fido().MakeNewCredentialAsync(
                    new MakeNewCredentialParams
                    {
                        AttestationResponse = response,
                        OriginalOptions = CredentialCreateOptions.FromJson(ceremony.Options),
                        // The store's unique index decides; PasskeyService checks it first for a clear answer.
                        IsCredentialIdUniqueToUserCallback = (_, _) => Task.FromResult(true),
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            return new NewPasskeyCredential(credential.Id, credential.PublicKey, credential.SignCount);
        }
        catch (Exception exception) when (IsRejection(exception))
        {
            LogRejected(logger, "registration", exception.GetType().Name, exception.Message);
            return null;
        }
    }

    public PasskeyCeremony BeginAssertion()
    {
        AssertionOptions requested = Fido().GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Required,
        });
        string json = requested.ToJson();
        return new PasskeyCeremony(json, _ceremonies.Protect(new Ceremony(json, UserId: null, Expiry())));
    }

    public byte[]? CredentialIdOf(string credentialJson)
    {
        try
        {
            return JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(credentialJson)?.RawId;
        }
        catch (Exception exception) when (IsRejection(exception))
        {
            return null;
        }
    }

    public async Task<long?> VerifyAssertionAsync(string transaction, string credentialJson, StoredPasskey stored, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stored);
        if (Open(transaction) is not { UserId: null } ceremony)
        {
            return null;
        }

        try
        {
            AuthenticatorAssertionRawResponse? response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(credentialJson);
            if (response is null)
            {
                return null;
            }

            VerifyAssertionResult verified = await Fido().MakeAssertionAsync(
                    new MakeAssertionParams
                    {
                        AssertionResponse = response,
                        OriginalOptions = AssertionOptions.FromJson(ceremony.Options),
                        StoredPublicKey = stored.PublicKey,
                        StoredSignatureCounter = (uint)stored.SignatureCounter,
                        IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                            Task.FromResult(args.UserHandle.AsSpan().SequenceEqual(stored.UserId.ToByteArray())),
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            return verified.SignCount;
        }
        catch (Exception exception) when (IsRejection(exception))
        {
            LogRejected(logger, "assertion", exception.GetType().Name, exception.Message);
            return null;
        }
    }

    private Fido2 Fido()
    {
        PasskeyRelyingPartyIdentity identity = Identity ?? throw new InvalidOperationException($"{PasskeyOptions.Section} names no relying party.");
        return new Fido2(new Fido2Configuration
        {
            RPID = identity.Id,
            RPName = identity.Name,
            Origins = new HashSet<string>(identity.Origins, StringComparer.Ordinal),
            Timeout = (uint)options.Value.CeremonyLifetime.TotalMilliseconds,
        });
    }

    private DateTimeOffset Expiry() => timeProvider.GetUtcNow() + options.Value.CeremonyLifetime;

    /// <summary>The ceremony the client sends back, if this platform sealed it and it has not expired.</summary>
    private Ceremony? Open(string transaction) =>
        string.IsNullOrEmpty(transaction) ? null
        : _ceremonies.Unprotect<Ceremony>(transaction) is { } ceremony && ceremony.ExpiresAt > timeProvider.GetUtcNow() ? ceremony
        : null;

    private static bool IsRejection(Exception exception) =>
        exception is Fido2VerificationException or JsonException or FormatException or ArgumentException or InvalidOperationException;

    /// <summary>A ceremony in flight: the options the device was given, whose passkey a registration is for, and until when.</summary>
    private sealed record Ceremony(string Options, Guid? UserId, DateTimeOffset ExpiresAt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Passkey {Ceremony} rejected ({Reason}): {Detail}")]
    private static partial void LogRejected(ILogger logger, string ceremony, string reason, string detail);
}
