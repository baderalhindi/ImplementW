using System.Net;
using System.Net.Http.Json;
using Npgsql;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.AuditActivity;

/// <summary>
/// TASK-033 acceptance criterion 1 and the first half of its validation check, through the API: every failed
/// authentication and every privileged action produces exactly one immutable audit event, and so does every
/// authorization refusal and every role change (CTL-25). Each request carries its own <c>X-Correlation-Id</c>, and its
/// events are found by it.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class AuditTrailTests(IdentityTestHost host) : IDisposable
{
    public void Dispose() => host.Clock.Reset();

    /// <summary>Every way a presented credential can fail, whether the directory, the platform or a token refuses it.</summary>
    public static TheoryData<string, string, string> FailedAuthentications() => new()
    {
        { "wrong password", "IdentityAccess.SignInFailed", "CREDENTIALS_REJECTED" },
        { "unknown person", "IdentityAccess.SignInFailed", "CREDENTIALS_REJECTED" },
        { "no platform account", "IdentityAccess.SignInFailed", "NO_PLATFORM_ACCOUNT" },
        { "disabled account", "IdentityAccess.SignInFailed", "ACCOUNT_INACTIVE" },
        { "suspended entity", "IdentityAccess.SignInFailed", "ACCOUNT_INACTIVE" },
        { "forged SSO code", "IdentityAccess.SignInFailed", "CREDENTIALS_REJECTED" },
        { "forged MFA token", "IdentityAccess.SignInFailed", "TOKEN_INVALID" },
        { "wrong second factor", "IdentityAccess.SignInFailed", "SECOND_FACTOR_REJECTED" },
        { "forged refresh token", "IdentityAccess.SessionRefreshFailed", "TOKEN_INVALID" },
        { "wrong step-up code", "IdentityAccess.StepUpFailed", "SECOND_FACTOR_REJECTED" },
        { "forged access token", "IdentityAccess.AccessTokenRejected", "TOKEN_INVALID" },
        { "expired access token", "IdentityAccess.AccessTokenRejected", "TOKEN_EXPIRED" },
        { "directory unreachable", "IdentityAccess.SignInFailed", "PROVIDER_UNAVAILABLE" },
    };

    [Theory]
    [MemberData(nameof(FailedAuthentications))]
    public async Task EveryFailedAuthenticationProducesOneAuditEvent(string failure, string eventType, string reason)
    {
        Guid correlationId = Guid.NewGuid();
        await FailAsync(failure, correlationId);

        List<string> failed = [.. (await host.Database.EventsAsync(correlationId)).Where(e => e.Contains("|FAILED|", StringComparison.Ordinal))];
        string only = Assert.Single(failed);
        Assert.StartsWith($"AUTHENTICATION|{eventType}|FAILED|", only, StringComparison.Ordinal);
        Assert.Contains($"failure_reason=>{reason}", only, StringComparison.Ordinal);
    }

    /// <summary>A failed password sign-in names the account it was tried against by the name typed, and no actor: nobody proved who they were.</summary>
    [Fact]
    public async Task AFailedPasswordSignInRecordsTheNameTypedAndNoActor()
    {
        Guid correlationId = Guid.NewGuid();
        using HttpClient client = host.Api.CreateClient().WithCorrelationId(correlationId);
        (await client.SignInAsync("local.r04", TestDirectory.PersonPassword)).Dispose();

        Assert.Equal(
            [$"AUTHENTICATION|IdentityAccess.SignInFailed|FAILED||{IdentityDatabase.UserId(4)} [authentication_method=>DIRECTORY, failure_reason=>ACCOUNT_INACTIVE, username=>local.r04]"],
            await host.Database.EventsAsync(correlationId));
    }

    [Fact]
    public async Task ASuccessfulSignInIsAuditedWithItsUser()
    {
        Guid correlationId = Guid.NewGuid();
        using HttpClient client = host.Api.CreateClient().WithCorrelationId(correlationId);
        await client.SignInOrFailAsync(2);

        Assert.Equal(
            [$"AUTHENTICATION|IdentityAccess.SignInSucceeded|SUCCESS|{IdentityDatabase.UserId(2)}|{IdentityDatabase.UserId(2)} [authentication_method=>DIRECTORY, multi_factor=>false, username=>local.r02]"],
            await host.Database.EventsAsync(correlationId));
    }

    /// <summary>The validation check's role change, and the privileged actions around it, each with actor, subject and change.</summary>
    [Fact]
    public async Task PrivilegedActionsAndRoleChangesAreAuditedWithTheirChange()
    {
        string administrator = IdentityDatabase.UserId(1);
        string user = IdentityDatabase.UserId(5);
        using HttpClient signIn = host.Api.CreateClient();
        string token = (await signIn.SignInOrFailAsync(1)).AccessToken;
        Guid? assignmentId = null;
        try
        {
            Assert.Equal(
                [$"PRIVILEGED_ACTION|IdentityAccess.UserDisabled|SUCCESS|{administrator}|{user} [status=ACTIVE>DISABLED]"],
                await ActAsync(client => client.PostAsync($"{AdministrationApi.Users}/{user}/disable", token)));
            Assert.Equal(
                [$"PRIVILEGED_ACTION|IdentityAccess.UserActivated|SUCCESS|{administrator}|{user} [status=DISABLED>ACTIVE]"],
                await ActAsync(client => client.PostAsync($"{AdministrationApi.Users}/{user}/activate", token)));

            Guid assigned = Guid.NewGuid();
            using (HttpClient client = host.Api.CreateClient().WithCorrelationId(assigned))
            {
                using HttpResponseMessage created = await client.PostAsync(
                    AdministrationApi.AccessRelationships, token, new { userId = user, permissionProfileVersionId = IdentityDatabase.ProfileVersionId(2) });
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
                assignmentId = AdministrationApi.IdOf(await created.ReadObjectAsync());
            }

            string roleAssigned = Assert.Single(await host.Database.EventsAsync(assigned));
            Assert.StartsWith($"PERMISSION_CHANGE|IdentityAccess.RoleAssigned|SUCCESS|{administrator}|{assignmentId} [", roleAssigned, StringComparison.Ordinal);
            Assert.Contains("role_code=>R02", roleAssigned, StringComparison.Ordinal);
            Assert.Contains($"user_id=>{user}", roleAssigned, StringComparison.Ordinal);

            string roleEnded = Assert.Single(await ActAsync(client => client.PostAsync($"{AdministrationApi.AccessRelationships}/{assignmentId}/end", token)));
            Assert.StartsWith($"PERMISSION_CHANGE|IdentityAccess.RoleAssignmentEnded|SUCCESS|{administrator}|{assignmentId} [", roleEnded, StringComparison.Ordinal);
            Assert.Contains("end_reason=>MANUAL", roleEnded, StringComparison.Ordinal);
            Assert.Contains("status=ACTIVE>ENDED", roleEnded, StringComparison.Ordinal);
        }
        finally
        {
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET status = 'ACTIVE', disabled_at = NULL WHERE id = '{user}'");
            if (assignmentId is { } id)
            {
                await host.Database.ExecuteAsync($"DELETE FROM identity_access.access_relationship WHERE id = '{id}'");
            }
        }
    }

    /// <summary>The change and its event are one unit of work: a change refused at save (a stale If-Match) leaves no event.</summary>
    [Fact]
    public async Task AChangeThatIsNotSavedLeavesNoAuditEvent()
    {
        using HttpClient signIn = host.Api.CreateClient();
        string token = (await signIn.SignInOrFailAsync(1)).AccessToken;

        IReadOnlyList<string> events = await ActAsync(client => client.PostAsync($"{AdministrationApi.Users}/{IdentityDatabase.UserId(5)}/disable", token, ifMatch: "\"1\""));

        Assert.Empty(events);
    }

    /// <summary>A contact detail that changes is recorded as changed, never copied into the audit store.</summary>
    [Fact]
    public async Task AChangedEmailIsRecordedAsWithheld()
    {
        string user = IdentityDatabase.UserId(6);
        using HttpClient signIn = host.Api.CreateClient();
        string token = (await signIn.SignInOrFailAsync(1)).AccessToken;
        using HttpClient reader = host.Api.CreateClient();
        using HttpResponseMessage current = await reader.GetAsync($"{AdministrationApi.Users}/{user}", token);
        var body = await current.ReadObjectAsync();
        object changes = new
        {
            username = body["username"]!.GetValue<string>(),
            displayName = body["displayName"]!.GetValue<string>(),
            email = "changed.r06@identity.test",
            preferredLanguage = body["preferredLanguage"]!.GetValue<string>(),
            directorySubjectId = body["directorySubjectId"]!.GetValue<string>(),
            jobTitle = body["jobTitle"]?.GetValue<string>(),
        };
        try
        {
            string updated = Assert.Single(await ActAsync(client => client.PutAsync($"{AdministrationApi.Users}/{user}", token, changes, AdministrationApi.ETagOf(current))));

            Assert.EndsWith("[email=[WITHHELD]>[WITHHELD]]", updated, StringComparison.Ordinal);
            Assert.Empty(await host.Database.QueryAsync(
                "SELECT id::text FROM audit_activity.audit_event_attribute WHERE old_value LIKE '%identity.test%' OR new_value LIKE '%identity.test%'"));
        }
        finally
        {
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET email = 'r06@identity.test' WHERE id = '{user}'");
        }
    }

    [Fact]
    public async Task APermissionRefusalIsAudited()
    {
        using HttpClient signIn = host.Api.CreateClient();
        string token = (await signIn.SignInOrFailAsync(6)).AccessToken;

        Assert.Equal(
            [$"AUTHORIZATION_DENIAL|IdentityAccess.AccessDenied|DENIED|{IdentityDatabase.UserId(6)}| [decision_outcome=>FORBIDDEN, denial_reason=>NOT_GRANTED, permission_code=>USER_VIEW]"],
            await ActAsync(client => client.GetAsync(AdministrationApi.Users, token)));
    }

    [Fact]
    public async Task AStepUpRefusalIsAudited()
    {
        using HttpClient signIn = host.Api.CreateClient();
        string token = (await signIn.SignInOrFailAsync(1)).AccessToken;
        host.Clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(
            [$"AUTHORIZATION_DENIAL|IdentityAccess.StepUpRequired|DENIED|{IdentityDatabase.UserId(1)}| [operation=>IdentityAccess_TestIdentityIntegration]"],
            await ActAsync(client => client.PostWithTokenAsync(SessionApi.IdentityIntegrationTest, token)));
    }

    /// <summary>
    /// "Immutable" is enforced by the database for every role that can reach it, the application's own included: an event
    /// or attribute can be neither changed nor deleted, and neither table can be truncated.
    /// </summary>
    [Theory]
    [InlineData("UPDATE audit_activity.audit_event SET event_type = 'IdentityAccess.Nothing'")]
    [InlineData("DELETE FROM audit_activity.audit_event")]
    [InlineData("TRUNCATE audit_activity.audit_event CASCADE")]
    [InlineData("UPDATE audit_activity.audit_event_attribute SET new_value = 'changed'")]
    [InlineData("DELETE FROM audit_activity.audit_event_attribute")]
    [InlineData("TRUNCATE audit_activity.audit_event_attribute")]
    public async Task AuditRowsCannotBeChangedOrDeleted(string statement)
    {
        using (HttpClient client = host.Api.CreateClient())
        {
            (await client.SignInAsync("local.r02", "a-wrong-password")).Dispose();
        }

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(statement));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    /// <summary>Every event recorded by every test so far is linked to its predecessor and hashes to its own content.</summary>
    [Fact]
    public async Task TheHashChainIsIntact()
    {
        using (HttpClient client = host.Api.CreateClient())
        {
            (await client.SignInAsync("local.r02", "a-wrong-password")).Dispose();
            (await client.SignInAsync("local.r03", "a-wrong-password")).Dispose();
        }

        Assert.NotEmpty(await host.Database.QueryAsync("SELECT id::text FROM audit_activity.audit_event"));
        Assert.Empty(await host.Database.QueryAsync(AuditStore.BrokenChainLinks));
        Assert.Single(await host.Database.QueryAsync("SELECT id::text FROM audit_activity.audit_event WHERE previous_event_hash IS NULL"));
    }

    private async Task<IReadOnlyList<string>> ActAsync(Func<HttpClient, Task<HttpResponseMessage>> request)
    {
        Guid correlationId = Guid.NewGuid();
        using HttpClient client = host.Api.CreateClient().WithCorrelationId(correlationId);
        (await request(client)).Dispose();
        return await host.Database.EventsAsync(correlationId);
    }

    private async Task FailAsync(string failure, Guid correlationId)
    {
        using HttpClient client = host.Api.CreateClient().WithCorrelationId(correlationId);
        switch (failure)
        {
            case "wrong password":
                (await client.SignInAsync("local.r02", "a-wrong-password")).Dispose();
                break;
            case "unknown person":
                (await client.SignInAsync("local.nobody", TestDirectory.PersonPassword)).Dispose();
                break;
            case "no platform account":
                (await client.SignInAsync("local.unregistered", TestDirectory.PersonPassword)).Dispose();
                break;
            case "disabled account":
                (await client.SignInAsync("local.r04", TestDirectory.PersonPassword)).Dispose();
                break;
            case "suspended entity":
                (await client.SignInAsync("local.r07", TestDirectory.PersonPassword)).Dispose();
                break;
            case "forged SSO code":
                (await client.PostAsJsonAsync(SessionApi.SsoSessions, new { code = "forged-code", state = "forged-state", transaction = "forged" })).Dispose();
                break;
            case "forged MFA token":
                (await client.PostAsJsonAsync(SessionApi.MfaChallenge, new { mfaToken = "forged-mfa-token" })).Dispose();
                break;
            case "wrong second factor":
                string mfaToken = await client.MfaTokenOrFailAsync("local.r01");
                MfaChallenge challenge = await client.StartChallengeOrFailAsync(SessionApi.MfaChallenge, new { mfaToken });
                (await client.PostAsJsonAsync(SessionApi.MfaSessions, new { mfaToken, challengeId = challenge.ChallengeId, code = TestMultiFactorProvider.WrongCodeFor(challenge.ChallengeId) })).Dispose();
                break;
            case "forged refresh token":
                (await client.RefreshAsync("forged-refresh-token")).Dispose();
                break;
            case "wrong step-up code":
                Session session = await client.SignInOrFailAsync(1);
                MfaChallenge stepUp = await client.StartChallengeOrFailAsync(SessionApi.StepUpChallenge, new { session.RefreshToken });
                (await client.PostAsJsonAsync(SessionApi.StepUp, new { session.RefreshToken, challengeId = stepUp.ChallengeId, code = TestMultiFactorProvider.WrongCodeFor(stepUp.ChallengeId) })).Dispose();
                break;
            case "forged access token":
                (await client.GetWithTokenAsync(SessionApi.Current, "forged.access.token")).Dispose();
                break;
            case "expired access token":
                string accessToken = (await client.SignInOrFailAsync(2)).AccessToken;
                host.Clock.Advance(TimeSpan.FromMinutes(16));
                (await client.GetWithTokenAsync(SessionApi.Current, accessToken)).Dispose();
                break;
            case "directory unreachable":
                await using (IdentityApiFactory unreachable = host.CreateApi(new Dictionary<string, string?> { ["AD_LDAP_URL"] = "ldap://127.0.0.1:1/dc=pmplatform,dc=local" }))
                {
                    using HttpClient failing = unreachable.CreateClient().WithCorrelationId(correlationId);
                    (await failing.SignInAsync("local.r02", TestDirectory.PersonPassword)).Dispose();
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failure), failure, "No such failure.");
        }
    }
}
