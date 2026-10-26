using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// Nafath as the tests play it (TASK-068): a <see cref="TestIdentityProvider"/> under Nafath's own client registration,
/// whose ID tokens assert a national id as their subject and carry the identity attributes a provider might volunteer —
/// none of which the platform may keep.
/// </summary>
internal static class TestNafath
{
    public const string ClientId = "pmplatform-test-nafath-client";

    /// <summary>A test value, valid only against the in-process Nafath.</summary>
    public const string ClientSecret = "test-nafath-client-secret";

    public const string CallbackUrl = "http://localhost:5173/auth/nafath/callback";

    public const string ApplicationId = "pmplatform-test-nafath-app";

    /// <summary>The national id Nafath asserts as the subject. Synthetic: it belongs to no one.</summary>
    public const string NationalId = "1099887766";

    public const string GivenName = "NafathGivenNameFixture";

    public const string FamilyName = "NafathFamilyNameFixture";

    public const string BirthDate = "1985-03-17";

    /// <summary>Every identity attribute the test Nafath hands over, as the database and the logs would hold them if kept.</summary>
    public static readonly string[] IdentityAttributes = [NationalId, GivenName, FamilyName, BirthDate];

    public static async Task<TestIdentityProvider> StartAsync()
    {
        TestIdentityProvider nafath = await TestIdentityProvider.StartAsync(ClientId, ClientSecret, CallbackUrl);
        Reset(nafath);
        return nafath;
    }

    /// <summary>Back to a provider that is up, answers honestly, and identifies the synthetic person.</summary>
    public static void Reset(TestIdentityProvider nafath)
    {
        nafath.NextSubject = NationalId;
        nafath.NonceOverride = null;
        nafath.Unavailable = false;
        nafath.ExtraClaims = new Dictionary<string, object>
        {
            ["national_id"] = NationalId,
            ["given_name"] = GivenName,
            ["family_name"] = FamilyName,
            ["birthdate"] = BirthDate,
        };
    }

    /// <summary>
    /// The suite back as the shared API expects it: Nafath honest and up, the SSO provider on its default person, the clock
    /// real, and local.r08 neither verified nor enrolled in MFA and last changed by the seed (a test that verifies them, or enrols
    /// them on the way, puts it back).
    /// </summary>
    public static async Task ResetAsync(IdentityTestHost host)
    {
        Reset(host.Nafath);
        host.IdentityProvider.NextSubject = TestDirectory.Subject(1);
        host.Clock.Reset();
        await host.Database.ExecuteAsync($"""
            UPDATE identity_access."user"
            SET nafath_verification_reference = NULL, nafath_verified_at = NULL, mfa_enrolled_at = NULL, updated_by = '{IdentityDatabase.SeedPrincipalId}'
            WHERE id = '{IdentityDatabase.UserId(8)}'
            """);
    }

    /// <summary>The API's settings for Nafath at <paramref name="nafath"/>, with the feature on.</summary>
    public static Dictionary<string, string?> Settings(TestIdentityProvider nafath) => new()
    {
        ["Identity:Nafath:Enabled"] = "true",
        ["Identity:Nafath:Authority"] = nafath.Authority.AbsoluteUri,
        ["Identity:Nafath:RequireHttpsMetadata"] = "false",
        ["NAFATH_CLIENT_ID"] = ClientId,
        ["NAFATH_CLIENT_SECRET"] = ClientSecret,
        ["NAFATH_CALLBACK_URL"] = CallbackUrl,
        ["NAFATH_APP_ID"] = ApplicationId,
    };

    /// <summary>
    /// What a browser does in a verification: asks the API where to go with <paramref name="verificationToken"/>, follows
    /// Nafath's redirect back to the callback URL, and returns what it came back with.
    /// </summary>
    public static async Task<NafathCallback> VerifyInBrowserAsync(HttpClient client, string verificationToken)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(SessionApi.IdentityVerificationAuthorization, new { identityVerificationToken = verificationToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        NafathAuthorization authorization = await response.ReadAsync<NafathAuthorization>();

        using HttpClientHandler handler = new() { AllowAutoRedirect = false };
        using HttpClient browser = new(handler);
        using HttpResponseMessage redirect = await browser.GetAsync(authorization.AuthorizationUrl);
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Uri location = redirect.Headers.Location!;
        Assert.StartsWith(CallbackUrl, location.AbsoluteUri, StringComparison.Ordinal);

        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(location.Query);
        return new NafathCallback(authorization.AuthorizationUrl, query["code"]!, query["state"]!, authorization.Transaction);
    }

    /// <summary>Completes the verification with what the browser came back with.</summary>
    public static Task<HttpResponseMessage> CompleteAsync(HttpClient client, string verificationToken, NafathCallback callback) =>
        client.PostAsJsonAsync(
            SessionApi.IdentityVerificationSessions,
            new { identityVerificationToken = verificationToken, code = callback.Code, state = callback.State, transaction = callback.Transaction });
}

internal sealed record NafathAuthorization(Uri AuthorizationUrl, string Transaction);

internal sealed record NafathCallback(Uri AuthorizationUrl, string Code, string State, string Transaction);
