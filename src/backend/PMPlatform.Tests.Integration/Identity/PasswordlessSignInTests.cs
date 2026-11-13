using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Authentication;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// The 13 Nov 2026 review's sign-ins without a password: a passkey on the person's device (Face ID, Touch ID), which passes
/// both factors, and a code by SMS to their verified mobile number, which passes the first. The passkeys are real WebAuthn
/// answers from <see cref="TestPasskeyDevice"/>, verified by the API as a phone's would be.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class PasswordlessSignInTests(IdentityTestHost host) : IAsyncLifetime
{
    private const string Origin = "https://pmplatform.test";
    private const string RelyingPartyId = "pmplatform.test";
    private const string Passkeys = "/api/v1/users/current/passkeys";
    private const string RegistrationOptions = "/api/v1/users/current/passkeys/registration-options";
    private const string PasskeyChallenge = "/api/v1/sessions/passkey-challenge";
    private const string PasskeySessions = "/api/v1/sessions/passkey";
    private const string SmsChallenge = "/api/v1/sessions/sms-code-challenge";
    private const string SmsSessions = "/api/v1/sessions/sms";
    private const string OwnMobileNumber = "/api/v1/users/current/mobile-number";
    private const string Mobile = "+966500000003";

    /// <summary>A valid National ID nobody holds.</summary>
    private const string NobodysNationalId = "1000000016";

    /// <summary>Valid National ID and Iqama numbers (Luhn) for the people these tests sign in.</summary>
    private static readonly Dictionary<int, string> NationalIds = new() { [1] = "1000000008", [3] = "1000000032", [6] = "2000000063" };

    private readonly TestSmsCodeSender _sms = new();
    private IdentityApiFactory _api = null!;

    public async Task InitializeAsync()
    {
        _api = host.CreateApi(
            new Dictionary<string, string?>
            {
                ["Identity:Passkeys:Origins:0"] = Origin,
                ["Identity:Session:AccessTokenLifetime"] = "01:00:00",
            },
            services =>
            {
                services.RemoveAll<ISmsCodeSender>();
                services.AddSingleton<ISmsCodeSender>(_sms);
            });
        INationalIdProtector protector = _api.Services.GetRequiredService<INationalIdProtector>();
        foreach ((int person, string nationalId) in NationalIds)
        {
            await host.Database.ExecuteAsync(
                $"UPDATE identity_access.\"user\" SET national_id_sealed = '{protector.Seal(nationalId)}', national_id_fingerprint = '{protector.Fingerprint(nationalId)}', national_id_last_digits = '{nationalId[^4..]}' WHERE id = '{IdentityDatabase.UserId(person)}'");
        }
    }

    public async Task DisposeAsync()
    {
        host.Clock.Reset();
        await host.Database.ExecuteAsync("DELETE FROM identity_access.user_passkey; DELETE FROM identity_access.sms_code;");
        await host.Database.ExecuteAsync(
            $"UPDATE identity_access.\"user\" SET national_id_sealed = NULL, national_id_fingerprint = NULL, national_id_last_digits = NULL WHERE id IN ({string.Join(", ", NationalIds.Keys.Select(p => $"'{IdentityDatabase.UserId(p)}'"))})");
        await host.Database.ExecuteAsync(
            $"UPDATE identity_access.\"user\" SET mobile_number = NULL, mobile_verified_at = NULL WHERE mobile_number = '{Mobile}'");
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task TheSignInOptionsOfferPasskeysAndSmsWhereTheyAreConfigured()
    {
        using HttpClient client = _api.CreateClient();

        SignInOptionsWithPasswordless options = await (await client.GetAsync(SessionApi.SignInOptions)).ReadAsync<SignInOptionsWithPasswordless>();

        Assert.True(options.PasskeySignInAvailable);
        Assert.True(options.SmsSignInAvailable);
    }

    /// <summary>R01 requires MFA; a passkey is the device plus the person's biometric, so no authenticator code follows it.</summary>
    [Fact]
    public async Task APasskeyAddedOnTheAccountSignsThePersonInWithBothFactors()
    {
        using HttpClient client = _api.CreateClient();
        using TestPasskeyDevice device = new(Origin, RelyingPartyId);
        Session signedIn = await client.SignInOrFailAsync(1);
        await RegisterAsync(client, signedIn.AccessToken, device, "iPhone");

        using HttpResponseMessage response = await SignInWithPasskeyAsync(client, device, NationalIds[1]);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Session session = await response.ReadAsync<Session>();
        Assert.Equal(Guid.Parse(IdentityDatabase.UserId(1)), session.User.Id);
        Assert.Equal("PASSKEY", session.User.AuthenticationMethod);
        Assert.True(session.User.MultiFactorAuthenticated);
        JsonObject listed = await (await client.GetAsync(Passkeys, session.AccessToken)).ReadObjectAsync();
        JsonNode passkey = Assert.Single(listed["items"]!.AsArray())!;
        Assert.Equal("iPhone", (string?)passkey["name"]);
        Assert.NotNull(passkey["lastUsedAt"]);
    }

    [Fact]
    public async Task ASignatureThatDoesNotVerifyIsNoSignIn()
    {
        using HttpClient client = _api.CreateClient();
        using TestPasskeyDevice device = new(Origin, RelyingPartyId);
        await RegisterAsync(client, (await client.SignInOrFailAsync(3)).AccessToken, device, "Mac");

        using HttpResponseMessage response = await SignInWithPasskeyAsync(client, device, NationalIds[3], tamper: signature => [.. signature[..^1], (byte)(signature[^1] ^ 0x01)]);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A passkey made for another site never signs in here: the browser binds it to that site's origin.</summary>
    [Fact]
    public async Task AnAnswerFromAnotherOriginIsRefused()
    {
        using HttpClient client = _api.CreateClient();
        using TestPasskeyDevice device = new(Origin, RelyingPartyId);
        string accessToken = (await client.SignInOrFailAsync(3)).AccessToken;
        PasskeyOptions started = await (await client.GetAsync(RegistrationOptions, accessToken)).ReadAsync<PasskeyOptions>();
        using TestPasskeyDevice phishing = new("https://pmplatform-test.example", RelyingPartyId);

        using HttpResponseMessage response = await client.PostAsync(
            Passkeys, accessToken, new { transaction = started.Transaction, credential = phishing.Create(started.Options), name = "Phish" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("IDENTITY_ACCESS_PASSKEY_REJECTED", (await response.ReadAsync<Problem>()).Code);
    }

    [Fact]
    public async Task AddingAPasskeyNeedsARecentSignIn()
    {
        using HttpClient client = _api.CreateClient();
        Session session = await client.SignInOrFailAsync(3);
        host.Clock.Advance(TimeSpan.FromMinutes(16));

        using HttpResponseMessage response = await client.GetAsync(RegistrationOptions, session.AccessToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("IDENTITY_ACCESS_RECENT_SIGN_IN_REQUIRED", (await response.ReadAsync<Problem>()).Code);
    }

    [Fact]
    public async Task ARemovedPasskeySignsNobodyIn()
    {
        using HttpClient client = _api.CreateClient();
        using TestPasskeyDevice device = new(Origin, RelyingPartyId);
        Session session = await client.SignInOrFailAsync(3);
        Guid passkeyId = await RegisterAsync(client, session.AccessToken, device, "Mac");

        using HttpResponseMessage removed = await client.PostAsync($"{Passkeys}/{passkeyId}/delete", session.AccessToken);
        using HttpResponseMessage response = await SignInWithPasskeyAsync(client, device, NationalIds[3]);

        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Empty((await removed.ReadObjectAsync())["items"]!.AsArray());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NobodyRemovesAnotherPersonsPasskey()
    {
        using HttpClient client = _api.CreateClient();
        using TestPasskeyDevice device = new(Origin, RelyingPartyId);
        Guid passkeyId = await RegisterAsync(client, (await client.SignInOrFailAsync(3)).AccessToken, device, "Mac");

        using HttpResponseMessage response = await client.PostAsync($"{Passkeys}/{passkeyId}/delete", (await client.SignInOrFailAsync(6)).AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SignInWithPasskeyAsync(client, device, NationalIds[3])).StatusCode);
    }

    /// <summary>The National ID names whose passkey may sign in: another person's ID, or nobody's, gets the same 401.</summary>
    [Fact]
    public async Task APasskeySignsInOnlyAsTheOwnerOfTheNationalIdGiven()
    {
        using HttpClient client = _api.CreateClient();
        using TestPasskeyDevice device = new(Origin, RelyingPartyId);
        await RegisterAsync(client, (await client.SignInOrFailAsync(3)).AccessToken, device, "Mac");

        using HttpResponseMessage asSomeoneElse = await SignInWithPasskeyAsync(client, device, NationalIds[6]);
        using HttpResponseMessage asNobody = await SignInWithPasskeyAsync(client, device, NobodysNationalId);
        using HttpResponseMessage asOwner = await SignInWithPasskeyAsync(client, device, NationalIds[3]);

        Assert.Equal(HttpStatusCode.Unauthorized, asSomeoneElse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, asNobody.StatusCode);
        Assert.Equal(HttpStatusCode.Created, asOwner.StatusCode);
    }

    [Theory]
    [InlineData("1000000009")]
    [InlineData("3000000000")]
    [InlineData("12345")]
    public async Task ANumberThatIsNoNationalIdIsRefusedBeforeAnything(string nationalId)
    {
        using HttpClient client = _api.CreateClient();

        using HttpResponseMessage passkey = await client.PostAsJsonAsync(PasskeyChallenge, new { nationalId });
        using HttpResponseMessage sms = await client.PostAsJsonAsync(SmsChallenge, new { nationalId });

        Assert.Equal(HttpStatusCode.BadRequest, passkey.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, sms.StatusCode);
        Assert.Empty(_sms.Sent);
    }

    [Fact]
    public async Task ACodeBySmsToAVerifiedNumberSignsThePersonIn()
    {
        await VerifiedMobileAsync(3);
        using HttpClient client = _api.CreateClient();

        SmsChallengeDetail challenge = await RequestCodeAsync(client, NationalIds[3]);
        using HttpResponseMessage response = await client.PostAsJsonAsync(SmsSessions, new { challengeId = challenge.ChallengeId, code = _sms.LastCodeTo(Mobile) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Session session = await response.ReadAsync<Session>();
        Assert.Equal(Guid.Parse(IdentityDatabase.UserId(3)), session.User.Id);
        Assert.Equal("SMS_CODE", session.User.AuthenticationMethod);
        Assert.False(session.User.MultiFactorAuthenticated);
    }

    /// <summary>An SMS code proves the phone only: R01 still gives the authenticator code.</summary>
    [Fact]
    public async Task ASmsCodeIsAFirstFactorOnly()
    {
        await VerifiedMobileAsync(1);
        using HttpClient client = _api.CreateClient();

        SmsChallengeDetail challenge = await RequestCodeAsync(client, NationalIds[1]);
        using HttpResponseMessage response = await client.PostAsJsonAsync(SmsSessions, new { challengeId = challenge.ChallengeId, code = _sms.LastCodeTo(Mobile) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Session session = await client.PassSecondFactorOrFailAsync((await response.ReadAsync<MfaPending>()).MfaToken);
        Assert.True(session.User.MultiFactorAuthenticated);
    }

    /// <summary>A National ID with no verified number, or nobody's, gets the same answer and no SMS, so the page tells nobody who has an account.</summary>
    [Fact]
    public async Task ANationalIdWithoutAVerifiedNumberGetsTheSameAnswerAndNoCode()
    {
        await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET mobile_number = '{Mobile}', mobile_verified_at = NULL WHERE id = '{IdentityDatabase.UserId(3)}'");
        using HttpClient client = _api.CreateClient();

        SmsChallengeDetail unverified = await RequestCodeAsync(client, NationalIds[3]);
        SmsChallengeDetail unknown = await RequestCodeAsync(client, NobodysNationalId);
        using HttpResponseMessage response = await client.PostAsJsonAsync(SmsSessions, new { challengeId = unverified.ChallengeId, code = "123456" });

        Assert.Empty(_sms.Sent);
        Assert.NotEqual(unverified.ChallengeId, unknown.ChallengeId);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ACodeStopsWorkingAfterFiveWrongTriesAndWorksOnce()
    {
        await VerifiedMobileAsync(3);
        using HttpClient client = _api.CreateClient();
        SmsChallengeDetail challenge = await RequestCodeAsync(client, NationalIds[3]);
        string code = _sms.LastCodeTo(Mobile);
        string wrong = code == "000000" ? "111111" : "000000";

        for (int attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(SmsSessions, new { challengeId = challenge.ChallengeId, code = wrong })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(SmsSessions, new { challengeId = challenge.ChallengeId, code })).StatusCode);

        host.Clock.Advance(TimeSpan.FromMinutes(2));
        SmsChallengeDetail next = await RequestCodeAsync(client, NationalIds[3]);
        string nextCode = _sms.LastCodeTo(Mobile);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(SmsSessions, new { challengeId = next.ChallengeId, code = nextCode })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(SmsSessions, new { challengeId = next.ChallengeId, code = nextCode })).StatusCode);
    }

    [Fact]
    public async Task AnExpiredCodeIsNoSignIn()
    {
        await VerifiedMobileAsync(3);
        using HttpClient client = _api.CreateClient();
        SmsChallengeDetail challenge = await RequestCodeAsync(client, NationalIds[3]);
        host.Clock.Advance(TimeSpan.FromMinutes(6));

        using HttpResponseMessage response = await client.PostAsJsonAsync(SmsSessions, new { challengeId = challenge.ChallengeId, code = _sms.LastCodeTo(Mobile) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>ADR-004's verification now works wherever SMS does: the code proves the number, and the number can then sign in.</summary>
    [Fact]
    public async Task APersonVerifiesTheirOwnNumberWithTheCodeSentToIt()
    {
        await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET mobile_number = '{Mobile}', mobile_verified_at = NULL WHERE id = '{IdentityDatabase.UserId(6)}'");
        using HttpClient client = _api.CreateClient();
        string accessToken = (await client.SignInOrFailAsync(6)).AccessToken;
        JsonObject before = await (await client.GetAsync(OwnMobileNumber, accessToken)).ReadObjectAsync();

        JsonObject challenge = await (await client.PostAsync(AdministrationApi.MobileVerificationChallenge, accessToken)).ReadObjectAsync();
        using HttpResponseMessage verified = await client.PostAsync(
            AdministrationApi.MobileVerification, accessToken, new { challengeId = (string?)challenge["challengeId"], code = _sms.LastCodeTo(Mobile) });

        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Equal(Mobile, (string?)(await verified.ReadObjectAsync())["mobileNumber"]);
        JsonObject after = await (await client.GetAsync(OwnMobileNumber, accessToken)).ReadObjectAsync();
        Assert.Equal(Mobile, (string?)before["mobileNumber"]);
        Assert.Null(before["verifiedAt"]);
        Assert.True((bool)before["verificationAvailable"]!);
        Assert.NotNull(after["verifiedAt"]);
        await RequestCodeAsync(client, NationalIds[6]);
        Assert.Equal(2, _sms.Sent.Count);
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, string accessToken, TestPasskeyDevice device, string name)
    {
        using HttpResponseMessage started = await client.GetAsync(RegistrationOptions, accessToken);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        PasskeyOptions options = await started.ReadAsync<PasskeyOptions>();

        using HttpResponseMessage created = await client.PostAsync(Passkeys, accessToken, new { transaction = options.Transaction, credential = device.Create(options.Options), name });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (Guid)(await created.ReadObjectAsync())["id"]!;
    }

    private static async Task<HttpResponseMessage> SignInWithPasskeyAsync(
        HttpClient client, TestPasskeyDevice device, string nationalId, Func<byte[], byte[]>? tamper = null)
    {
        using HttpResponseMessage started = await client.PostAsJsonAsync(PasskeyChallenge, new { nationalId });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        PasskeyOptions options = await started.ReadAsync<PasskeyOptions>();
        return await client.PostAsJsonAsync(PasskeySessions, new { transaction = options.Transaction, credential = device.Get(options.Options, tamper) });
    }

    private static async Task<SmsChallengeDetail> RequestCodeAsync(HttpClient client, string nationalId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(SmsChallenge, new { nationalId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<SmsChallengeDetail>();
    }

    private Task VerifiedMobileAsync(int person) =>
        host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET mobile_number = '{Mobile}', mobile_verified_at = now() WHERE id = '{IdentityDatabase.UserId(person)}'");

    private sealed record PasskeyOptions(JsonElement Options, string Transaction);

    private sealed record SmsChallengeDetail(Guid ChallengeId, DateTimeOffset ExpiresAt);

    private sealed record SignInOptionsWithPasswordless(bool SingleSignOnAvailable, bool PasswordResetAvailable, bool PasskeySignInAvailable, bool SmsSignInAvailable);
}
