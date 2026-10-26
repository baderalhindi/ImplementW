using System.Net.Http.Json;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.AuditActivity;

/// <summary>
/// TASK-033 acceptance criterion 2, the redaction test suite: no password, token, second-factor code or secret that
/// passes through sign-in, the second factor, step-up, refresh, administration or SIEM forwarding appears in any log
/// line, in any audit row, or in any event the SIEM receives. The SIEM token appears only in the Authorization header of
/// the requests that carry the events to the SIEM (CTL-26, CTL-27).
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class RedactionTests(IdentityTestHost host)
{
    [Fact]
    public async Task NoSecretReachesTheLogsTheAuditStoreOrTheSiem()
    {
        await using IdentityApiFactory api = host.CreateApi(host.Siem.Settings);
        Guid correlationId = Guid.NewGuid();
        using HttpClient client = api.CreateClient().WithCorrelationId(correlationId);
        List<string> secrets =
        [
            TestDirectory.PersonPassword, TestDirectory.BindPassword, TestIdentityProvider.ClientSecret, IdentityApiFactory.SigningKey,
            TestMultiFactorProvider.ApiKey, TestSiem.ApiToken,
        ];

        // Sign-in with a mistyped password and with a wrong second factor, then the right one.
        const string mistypedPassword = "a-mistyped-password-5518";
        secrets.Add(mistypedPassword);
        (await client.SignInAsync("local.r01", mistypedPassword)).Dispose();
        string mfaToken = await client.MfaTokenOrFailAsync("local.r01");
        secrets.Add(mfaToken);
        MfaChallenge wrong = await client.StartChallengeOrFailAsync(SessionApi.MfaChallenge, new { mfaToken });
        string wrongCode = TestMultiFactorProvider.WrongCodeFor(wrong.ChallengeId);
        secrets.Add($"\"{wrongCode}\"");
        (await client.PostAsJsonAsync(SessionApi.MfaSessions, new { mfaToken, challengeId = wrong.ChallengeId, code = wrongCode })).Dispose();
        MfaChallenge right = await client.StartChallengeOrFailAsync(SessionApi.MfaChallenge, new { mfaToken });
        string rightCode = TestMultiFactorProvider.CodeFor(right.ChallengeId);
        secrets.Add($"\"{rightCode}\"");
        using HttpResponseMessage secondFactor = await client.PostAsJsonAsync(SessionApi.MfaSessions, new { mfaToken, challengeId = right.ChallengeId, code = rightCode });
        Session session = await secondFactor.ReadAsync<Session>();
        secrets.AddRange([session.AccessToken, session.RefreshToken]);

        // Step-up, refresh, a privileged action and a refusal with the resulting tokens, and a forged token.
        Session steppedUp = await client.StepUpOrFailAsync(session.RefreshToken);
        secrets.AddRange([steppedUp.AccessToken, steppedUp.RefreshToken]);
        using (HttpResponseMessage refreshed = await client.RefreshAsync(steppedUp.RefreshToken))
        {
            Session next = await refreshed.ReadAsync<Session>();
            secrets.AddRange([next.AccessToken, next.RefreshToken]);
        }

        (await client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, steppedUp.AccessToken)).Dispose();
        (await client.GetWithTokenAsync(SessionApi.Current, session.AccessToken + "tampered")).Dispose();
        (await client.RefreshAsync(session.RefreshToken + "tampered")).Dispose();
        (await client.PostAsJsonAsync(SessionApi.SsoSessions, new { code = "forged-code", state = "forged-state", transaction = "forged" })).Dispose();

        // Everything this test caused is forwarded before anything is searched.
        Assert.NotNull(await host.Siem.WaitForEventAsync(correlationId, "IdentityAccess.SignInFailed", TimeSpan.FromSeconds(10)));
        Assert.NotNull(await host.Siem.WaitForEventAsync(correlationId, "IdentityAccess.IdentityIntegrationTested", TimeSpan.FromSeconds(10)));

        string logs = host.Logs.Text;
        string auditStore = string.Join('\n', await host.Database.QueryAsync("""
            SELECT row_to_json(e)::text FROM audit_activity.audit_event e
            UNION ALL SELECT row_to_json(a)::text FROM audit_activity.audit_event_attribute a
            """));
        List<TestSiem.ReceivedRequest> siemRequests = [.. host.Siem.Requests];
        string siemBodies = string.Join('\n', siemRequests.Select(r => r.Body));

        Assert.Contains(correlationId.ToString(), auditStore, StringComparison.Ordinal);
        Assert.Contains(correlationId.ToString(), siemBodies, StringComparison.Ordinal);
        Assert.All(secrets, secret =>
        {
            Assert.DoesNotContain(secret, logs, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, auditStore, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, siemBodies, StringComparison.Ordinal);
        });
        Assert.All(siemRequests, r => Assert.Equal($"Bearer {TestSiem.ApiToken}", r.Authorization));
    }
}
