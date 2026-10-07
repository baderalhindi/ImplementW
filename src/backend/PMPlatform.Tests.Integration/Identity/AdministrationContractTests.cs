using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// The FG-03 endpoint contract tests (TASK-031 validation check): every ADM-002–013 operation round-trips, and every refusal
/// is the api-conventions envelope with its catalogue status and code — validation (R-23/R-25), idempotency key (R-36),
/// concurrency (R-21), state (R-24) and scope (R-47).
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class AdministrationContractTests(IdentityTestHost host)
{
    [Fact]
    public async Task AUserIsCreatedReadListedAndEditedUnderTheETag()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(1)).AccessToken;
        string suffix = Suffix();

        using HttpResponseMessage created = await client.PostAsync(AdministrationApi.Users, token, InternalUser(suffix));
        JsonObject user = await created.ReadObjectAsync();
        string path = $"{AdministrationApi.Users}/{AdministrationApi.IdOf(user)}";
        using HttpResponseMessage read = await client.GetAsync(path, token);
        using HttpResponseMessage listed = await client.GetAsync($"{AdministrationApi.Users}?status=ACTIVE&userType=INTERNAL&q=admin.{suffix}&sort=username:desc", token);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(path, created.Headers.Location!.OriginalString);
        Assert.Equal(AdministrationApi.ETagOf(created), AdministrationApi.ETagOf(read));
        Assert.Equal(("INTERNAL", "ACTIVE", "ar"), (user["userType"]!.GetValue<string>(), user["status"]!.GetValue<string>(), user["preferredLanguage"]!.GetValue<string>()));
        Assert.Null(user["mobileVerifiedAt"]);
        JsonObject page = await listed.ReadObjectAsync();
        Assert.Equal((1, 25, 1), (page["page"]!.GetValue<int>(), page["pageSize"]!.GetValue<int>(), page["totalCount"]!.GetValue<int>()));

        object edit = new { username = $"admin.{suffix}", displayName = "Renamed", email = $"admin.{suffix}@identity.test", mobileNumber = "+966500000031", preferredLanguage = "en", directorySubjectId = $"subject-{suffix}" };
        using HttpResponseMessage withoutIfMatch = await client.PutAsync(path, token, edit, ifMatch: null);
        using HttpResponseMessage stale = await client.PutAsync(path, token, edit, ifMatch: "\"1\"");
        using HttpResponseMessage edited = await client.PutAsync(path, token, edit, AdministrationApi.ETagOf(read));
        using HttpResponseMessage sameETagAgain = await client.PutAsync(path, token, edit, AdministrationApi.ETagOf(read));

        await AssertProblemAsync(withoutIfMatch, HttpStatusCode.PreconditionRequired, "PRECONDITION_REQUIRED");
        await AssertProblemAsync(stale, HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED");
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.NotEqual(AdministrationApi.ETagOf(read), AdministrationApi.ETagOf(edited));
        Assert.Equal("Renamed", (await edited.ReadObjectAsync())["displayName"]!.GetValue<string>());
        await AssertProblemAsync(sameETagAgain, HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED");
    }

    /// <summary>R-23/R-25: 400 VALIDATION_FAILED lists every field and its code, and never the value supplied.</summary>
    [Fact]
    public async Task AMalformedRequestNamesEveryFieldAndNoValue()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(1)).AccessToken;

        using HttpResponseMessage response = await client.PostAsync(AdministrationApi.Users, token, new
        {
            userType = "EXTERNAL",
            username = "",
            displayName = "   ",
            email = "not-an-address",
            mobileNumber = "0500000000",
            preferredLanguage = "fr",
        });
        string body = await response.Content.ReadAsStringAsync();

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        Assert.Equal(
            ["username REQUIRED", "displayName REQUIRED", "email MALFORMED", "mobileNumber MALFORMED", "preferredLanguage ENUM_VALUE", "externalEntityId REQUIRED"],
            await response.ReadFieldErrorsAsync());
        Assert.DoesNotContain("0500000000", body, StringComparison.Ordinal);
        Assert.DoesNotContain("not-an-address", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("status=ACTIVE,GONE", "status ENUM_VALUE")]
    [InlineData("pageSize=201", "pageSize OUT_OF_RANGE")]
    [InlineData("page=0", "page OUT_OF_RANGE")]
    [InlineData("sort=email:asc", "sort ENUM_VALUE")]
    public async Task AMalformedQueryIsRefused(string query, string error)
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(1)).AccessToken;

        using HttpResponseMessage response = await client.GetAsync($"{AdministrationApi.Users}?{query}", token);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        Assert.Equal([error], await response.ReadFieldErrorsAsync());
    }

    /// <summary>R-36: a sensitive write without a uuid Idempotency-Key is refused before anything is written.</summary>
    [Theory]
    [InlineData("", "IDEMPOTENCY_KEY_REQUIRED")]
    [InlineData("not-a-uuid", "IDEMPOTENCY_KEY_INVALID")]
    public async Task ASensitiveWriteRequiresAnIdempotencyKey(string key, string code)
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(1)).AccessToken;
        string suffix = Suffix();

        using HttpResponseMessage response = await client.SendAsync(HttpMethod.Post, AdministrationApi.Users, token, InternalUser(suffix), idempotencyKey: key);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, code);
        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM identity_access.\"user\" WHERE username = 'admin.{suffix}'"));
    }

    [Fact]
    public async Task ATakenKeyAndARepeatedTransitionAreConflicts()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(1)).AccessToken;
        string suffix = Suffix();
        using HttpResponseMessage created = await client.PostAsync(AdministrationApi.Users, token, InternalUser(suffix));
        string path = $"{AdministrationApi.Users}/{AdministrationApi.IdOf(await created.ReadObjectAsync())}";

        // Only the username repeats; PostgreSQL reports the first unique index a row violates, so one key is tested at a time.
        using HttpResponseMessage duplicate = await client.PostAsync(AdministrationApi.Users, token, new
        {
            userType = "INTERNAL",
            username = $"admin.{suffix}",
            displayName = "Another",
            email = $"another.{suffix}@identity.test",
            directorySubjectId = $"another-{suffix}",
        });
        using HttpResponseMessage disabled = await client.PostAsync($"{path}/disable", token);
        using HttpResponseMessage disabledAgain = await client.PostAsync($"{path}/disable", token);
        using HttpResponseMessage selfDisable = await client.PostAsync($"{AdministrationApi.Users}/{IdentityDatabase.UserId(1)}/disable", token);

        await AssertProblemAsync(duplicate, HttpStatusCode.Conflict, IdentityAccessErrorCodes.DuplicateKey);
        Assert.Equal(["username DUPLICATE"], await duplicate.ReadFieldErrorsAsync());
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        await AssertProblemAsync(disabledAgain, HttpStatusCode.Conflict, "INVALID_TRANSITION");
        await AssertProblemAsync(selfDisable, HttpStatusCode.UnprocessableEntity, IdentityAccessErrorCodes.SelfAdministration);
    }

    [Theory]
    [InlineData(AdministrationApi.Users)]
    [InlineData(AdministrationApi.AccessRelationships)]
    [InlineData(AdministrationApi.Roles)]
    [InlineData(AdministrationApi.PermissionProfiles)]
    [InlineData(AdministrationApi.Departments)]
    [InlineData(AdministrationApi.ExternalEntities)]
    public async Task AnUnknownIdIsNotFound(string collection)
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(1)).AccessToken;

        using HttpResponseMessage response = await client.GetAsync($"{collection}/{Guid.NewGuid()}", token);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "NOT_FOUND");
    }

    /// <summary>
    /// ADR-013 through the API: an external user holds R04 on their entity's project with a named AHDA sponsor; a role change on
    /// that project ends the previous access; an internal-only role or a missing sponsor is refused with every rule named.
    /// </summary>
    [Fact]
    public async Task AnExternalUsersGrantsFollowTheParticipationModel()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(1)).AccessToken;
        string suffix = Suffix();
        using HttpResponseMessage created = await client.PostAsync(AdministrationApi.Users, token, new
        {
            userType = "EXTERNAL",
            username = $"entity.{suffix}",
            displayName = "Entity PM",
            email = $"entity.{suffix}@entity.test",
            jobTitle = "Project lead",
            externalEntityId = IdentityDatabase.ActiveEntityId,
        });
        Guid userId = AdministrationApi.IdOf(await created.ReadObjectAsync());
        string sponsor = IdentityDatabase.UserId(2);

        using HttpResponseMessage internalRole = await client.PostAsync(AdministrationApi.AccessRelationships, token, new
        {
            userId,
            permissionProfileVersionId = IdentityDatabase.ProfileVersionId(3),
            departmentId = IdentityDatabase.DepartmentId,
        });
        using HttpResponseMessage manager = await client.PostAsync(AdministrationApi.AccessRelationships, token, new
        {
            userId,
            permissionProfileVersionId = IdentityDatabase.ProfileVersionId(4),
            projectId = IdentityDatabase.EntityProjectId,
            sponsorUserId = sponsor,
        });
        string managerPath = $"{AdministrationApi.AccessRelationships}/{AdministrationApi.IdOf(await manager.ReadObjectAsync())}";
        using HttpResponseMessage contributor = await client.PostAsync(AdministrationApi.AccessRelationships, token, new
        {
            userId,
            permissionProfileVersionId = IdentityDatabase.ProfileVersionId(8),
            projectId = IdentityDatabase.EntityProjectId,
            sponsorUserId = sponsor,
        });
        using HttpResponseMessage managerAfter = await client.GetAsync(managerPath, token);
        using HttpResponseMessage listed = await client.GetAsync($"{AdministrationApi.AccessRelationships}?userId={userId}&status=ACTIVE", token);

        await AssertProblemAsync(internalRole, HttpStatusCode.UnprocessableEntity, IdentityAccessErrorCodes.ExternalGrantInvalid);
        Assert.Equal(
            ["permissionProfileVersionId NOT_EXTERNAL_ELIGIBLE", "departmentId NOT_ALLOWED", "sponsorUserId REQUIRED"],
            await internalRole.ReadFieldErrorsAsync());
        Assert.Equal(HttpStatusCode.Created, manager.StatusCode);
        JsonObject managerBody = await managerAfter.ReadObjectAsync();
        Assert.Equal(("R04", IdentityDatabase.ActiveEntityId, sponsor), (managerBody["roleCode"]!.GetValue<string>(), managerBody["externalEntityId"]!.GetValue<string>(), managerBody["sponsorUserId"]!.GetValue<string>()));
        Assert.Equal(HttpStatusCode.Created, contributor.StatusCode);
        Assert.Equal(("ENDED", "ROLE_CHANGE"), (managerBody["status"]!.GetValue<string>(), managerBody["endReason"]!.GetValue<string>()));
        JsonObject page = await listed.ReadObjectAsync();
        Assert.Equal("R08", Assert.Single(page["items"]!.AsArray())!["roleCode"]!.GetValue<string>());
    }

    [Fact]
    public async Task ADepartmentTreeIsBuiltEditedAndDeactivated()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(1)).AccessToken;
        string code = $"DEPT-{Suffix().ToUpperInvariant()}";

        using HttpResponseMessage parent = await client.PostAsync(AdministrationApi.Departments, token, new { code, name = new { ar = "إدارة", en = "Parent" } });
        string parentPath = $"{AdministrationApi.Departments}/{AdministrationApi.IdOf(await parent.ReadObjectAsync())}";
        Guid parentId = AdministrationApi.IdOf(await parent.ReadObjectAsync());
        using HttpResponseMessage child = await client.PostAsync(AdministrationApi.Departments, token, new { code = $"{code}-1", name = new { ar = "قسم", en = "Child" }, parentDepartmentId = parentId });
        Guid childId = AdministrationApi.IdOf(await child.ReadObjectAsync());
        using HttpResponseMessage duplicate = await client.PostAsync(AdministrationApi.Departments, token, new { code, name = new { ar = "إدارة", en = "Again" } });
        using HttpResponseMessage cycle = await client.PutAsync(parentPath, token, new { name = new { ar = "إدارة", en = "Parent" }, parentDepartmentId = childId }, AdministrationApi.ETagOf(parent));
        using HttpResponseMessage children = await client.GetAsync($"{AdministrationApi.Departments}?parentDepartmentId={parentId}", token);
        using HttpResponseMessage deactivated = await client.PostAsync($"{parentPath}/deactivate", token, ifMatch: AdministrationApi.ETagOf(parent));
        using HttpResponseMessage noLabel = await client.PostAsync(AdministrationApi.Departments, token, new { code = "lower-case", name = new { ar = "", en = "Label" } });

        Assert.Equal(HttpStatusCode.Created, child.StatusCode);
        await AssertProblemAsync(duplicate, HttpStatusCode.Conflict, IdentityAccessErrorCodes.DuplicateKey);
        Assert.Equal(["code DUPLICATE"], await duplicate.ReadFieldErrorsAsync());
        await AssertProblemAsync(cycle, HttpStatusCode.UnprocessableEntity, IdentityAccessErrorCodes.DepartmentCycle);
        Assert.Equal(childId, AdministrationApi.IdOf(Assert.Single((await children.ReadObjectAsync())["items"]!.AsArray())!.AsObject()));
        Assert.False((await deactivated.ReadObjectAsync())["isActive"]!.GetValue<bool>());
        Assert.Equal(["code MALFORMED", "name.ar REQUIRED"], await noLabel.ReadFieldErrorsAsync());
    }

    [Fact]
    public async Task AnEntityIsSuspendedAndRetiredAndThenTakesNoWrite()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(1)).AccessToken;
        string entityType = (await host.Database.QueryAsync("""
            SELECT i.id::text FROM master_data_config.master_data_item i
            JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id AND c.code = 'EXTERNAL_ENTITY_TYPE' ORDER BY i.code LIMIT 1
            """))[0];
        object representation = new { name = new { ar = "جهة", en = "Entity" }, entityTypeItemId = entityType, sponsorUserId = IdentityDatabase.UserId(2) };

        using HttpResponseMessage created = await client.PostAsync(AdministrationApi.ExternalEntities, token, new
        {
            code = $"ENT-{Suffix().ToUpperInvariant()}",
            name = new { ar = "جهة", en = "Entity" },
            entityTypeItemId = entityType,
            sponsorUserId = IdentityDatabase.UserId(2),
        });
        string path = $"{AdministrationApi.ExternalEntities}/{AdministrationApi.IdOf(await created.ReadObjectAsync())}";
        using HttpResponseMessage wrongType = await client.PostAsync(AdministrationApi.ExternalEntities, token, new
        {
            code = $"ENT-{Suffix().ToUpperInvariant()}",
            name = new { ar = "جهة", en = "Entity" },
            entityTypeItemId = Guid.NewGuid(),
            sponsorUserId = IdentityDatabase.UserId(8),
        });
        using HttpResponseMessage suspended = await client.PostAsync($"{path}/suspend", token);
        using HttpResponseMessage retired = await client.PostAsync($"{path}/retire", token);
        using HttpResponseMessage editRetired = await client.PutAsync(path, token, representation, AdministrationApi.ETagOf(retired));
        using HttpResponseMessage reactivate = await client.PostAsync($"{path}/activate", token);

        Assert.Equal(("ACTIVE", "SUSPENDED", "RETIRED"), (
            (await created.ReadObjectAsync())["status"]!.GetValue<string>(),
            (await suspended.ReadObjectAsync())["status"]!.GetValue<string>(),
            (await retired.ReadObjectAsync())["status"]!.GetValue<string>()));
        Assert.Equal(["entityTypeItemId NOT_FOUND", "sponsorUserId INACTIVE"], await wrongType.ReadFieldErrorsAsync());
        await AssertProblemAsync(editRetired, HttpStatusCode.Conflict, "TERMINAL_STATE");
        await AssertProblemAsync(reactivate, HttpStatusCode.Conflict, "TERMINAL_STATE");
    }

    [Fact]
    public async Task RolesPermissionsAndProfilesAreReadAndARoleIsRenamed()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(1)).AccessToken;
        const string r01RoleId = "00000000-0000-4000-8000-000000000001";
        const string r01ProfileId = "00000000-0001-4000-8000-000000000001";
        const string r06RoleId = "00000000-0000-4000-8000-000000000006";

        using HttpResponseMessage roles = await client.GetAsync(AdministrationApi.Roles, token);
        using HttpResponseMessage permissions = await client.GetAsync(AdministrationApi.Permissions, token);
        using HttpResponseMessage profiles = await client.GetAsync($"{AdministrationApi.PermissionProfiles}?baseRoleId={r01RoleId}", token);
        using HttpResponseMessage profile = await client.GetAsync($"{AdministrationApi.PermissionProfiles}/{r01ProfileId}", token);
        using HttpResponseMessage role = await client.GetAsync($"{AdministrationApi.Roles}/{r06RoleId}", token);
        JsonObject original = await role.ReadObjectAsync();
        try
        {
            using HttpResponseMessage renamed = await client.PutAsync($"{AdministrationApi.Roles}/{r06RoleId}", token, new { name = new { ar = "مستعرض فقط", en = "Read-only viewer" } }, AdministrationApi.ETagOf(role));

            Assert.Equal(["R01", "R02", "R03", "R04", "R05", "R06", "R07", "R08"], (await roles.ReadArrayAsync()).Select(r => r!["code"]!.GetValue<string>()));
            Assert.Equal(64, (await permissions.ReadArrayAsync()).Count);
            Assert.Equal("R01-DEFAULT", Assert.Single((await profiles.ReadObjectAsync())["items"]!.AsArray())!["code"]!.GetValue<string>());
            JsonObject version = (await profile.ReadObjectAsync())["versions"]!.AsArray()[0]!.AsObject();
            Assert.Equal(("PUBLISHED", 16), (version["lifecycleState"]!.GetValue<string>(), version["grants"]!.AsArray().Count));
            Assert.Equal("R06-DEFAULT", Assert.Single(original["profiles"]!.AsArray())!["code"]!.GetValue<string>());
            Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
            Assert.Equal("Read-only viewer", (await renamed.ReadObjectAsync())["name"]!["en"]!.GetValue<string>());
        }
        finally
        {
            await host.Database.ExecuteAsync($"UPDATE identity_access.role SET name_ar = '{original["name"]!["ar"]}', name_en = '{original["name"]!["en"]}' WHERE id = '{r06RoleId}'");
        }
    }

    /// <summary>ADR-004: without an SMS provider verification is unavailable, and no number is ever marked verified.</summary>
    [Fact]
    public async Task WithoutAnSmsProviderAMobileNumberCannotBeVerified()
    {
        using HttpClient client = host.Api.CreateClient();
        await SetViewerMobileAsync("+966500000061");
        try
        {
            string token = (await client.SignInOrFailAsync(6)).AccessToken;

            using HttpResponseMessage challenge = await client.PostAsync(AdministrationApi.MobileVerificationChallenge, token);

            await AssertProblemAsync(challenge, HttpStatusCode.ServiceUnavailable, "UNAVAILABLE");
            Assert.Equal([""], await host.Database.QueryAsync($"SELECT coalesce(mobile_verified_at::text, '') FROM identity_access.\"user\" WHERE id = '{IdentityDatabase.UserId(6)}'"));
        }
        finally
        {
            await SetViewerMobileAsync(null);
        }
    }

    /// <summary>
    /// ADR-004's verification step with a provider in place: only the right code for the number stored verifies it, only the
    /// holder's own session can, and an administrator's later change of number unverifies it again.
    /// </summary>
    [Fact]
    public async Task TheHolderOfTheNumberVerifiesItAndAChangedNumberIsUnverified()
    {
        FixedCodeVerifier verifier = new();
        await using WebApplicationFactory<Program> api = host.Api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMobileNumberVerifier>();
            services.AddSingleton<IMobileNumberVerifier>(verifier);
        }));
        using HttpClient client = api.CreateClient();
        await SetViewerMobileAsync("+966500000061");
        try
        {
            string token = (await client.SignInOrFailAsync(6)).AccessToken;
            string administrator = (await client.SignInOrFailAsync(1)).AccessToken;

            using HttpResponseMessage challenge = await client.PostAsync(AdministrationApi.MobileVerificationChallenge, token);
            string challengeId = (await challenge.ReadObjectAsync())["challengeId"]!.GetValue<string>();
            using HttpResponseMessage wrong = await client.PostAsync(AdministrationApi.MobileVerification, token, new { challengeId, code = "000000" });
            using HttpResponseMessage right = await client.PostAsync(AdministrationApi.MobileVerification, token, new { challengeId, code = FixedCodeVerifier.Code });
            using HttpResponseMessage again = await client.PostAsync(AdministrationApi.MobileVerificationChallenge, token);

            await AssertProblemAsync(wrong, HttpStatusCode.UnprocessableEntity, IdentityAccessErrorCodes.MobileVerificationFailed);
            Assert.Equal(HttpStatusCode.OK, right.StatusCode);
            Assert.Equal(("+966500000061", IdentityDatabase.UserId(6)), (verifier.SentTo, verifier.SentFor.ToString()));
            await AssertProblemAsync(again, HttpStatusCode.Conflict, "INVALID_TRANSITION");

            using IServiceScope scope = api.Services.CreateScope();
            IUserContactDirectory contacts = scope.ServiceProvider.GetRequiredService<IUserContactDirectory>();
            Assert.Equal("+966500000061", await contacts.FindVerifiedMobileNumberAsync(Guid.Parse(IdentityDatabase.UserId(6)), CancellationToken.None));

            string path = $"{AdministrationApi.Users}/{IdentityDatabase.UserId(6)}";
            using HttpResponseMessage read = await client.GetAsync(path, administrator);
            JsonObject user = await read.ReadObjectAsync();
            using HttpResponseMessage changed = await client.PutAsync(path, administrator, new
            {
                username = user["username"]!.GetValue<string>(),
                displayName = user["displayName"]!.GetValue<string>(),
                email = user["email"]!.GetValue<string>(),
                mobileNumber = "+966500000062",
                preferredLanguage = user["preferredLanguage"]!.GetValue<string>(),
                directorySubjectId = user["directorySubjectId"]!.GetValue<string>(),
                jobTitle = user["jobTitle"]!.GetValue<string>(),
            }, AdministrationApi.ETagOf(read));

            Assert.NotNull(user["mobileVerifiedAt"]);
            Assert.Null((await changed.ReadObjectAsync())["mobileVerifiedAt"]);
            Assert.Null(await contacts.FindVerifiedMobileNumberAsync(Guid.Parse(IdentityDatabase.UserId(6)), CancellationToken.None));
        }
        finally
        {
            await SetViewerMobileAsync(null);
        }
    }

    /// <summary>R-23: the platform envelope, with the catalogue status and code, the request path, a correlation id and the key echoed.</summary>
    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonObject problem = await response.ReadObjectAsync();
        Assert.Equal(code, problem["code"]!.GetValue<string>());
        Assert.Equal((int)status, problem["status"]!.GetValue<int>());
        Assert.Equal($"urn:pmplatform:problem:{code.ToLowerInvariant().Replace('_', '-')}", problem["type"]!.GetValue<string>());
        Assert.StartsWith("/api/v1/", problem["instance"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(Guid.TryParse(problem["correlationId"]!.GetValue<string>(), out _));
        Assert.NotNull(problem["timestamp"]);
        Assert.True(problem.ContainsKey("idempotencyKey"));
    }

    private static string Suffix() => Guid.NewGuid().ToString("N")[..10];

    private static object InternalUser(string suffix) => new
    {
        userType = "INTERNAL",
        username = $"admin.{suffix}",
        displayName = "Admin Test",
        email = $"admin.{suffix}@identity.test",
        directorySubjectId = $"subject-{suffix}",
    };

    private Task SetViewerMobileAsync(string? mobileNumber) =>
        host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET mobile_number = {(mobileNumber is null ? "NULL" : $"'{mobileNumber}'")}, mobile_verified_at = NULL WHERE id = '{IdentityDatabase.UserId(6)}'");

    /// <summary>An SMS verification provider that sends one fixed code and accepts it only for the user and number it was sent to.</summary>
    private sealed class FixedCodeVerifier : IMobileNumberVerifier
    {
        public const string Code = "135790";

        public Guid SentFor { get; private set; }

        public string? SentTo { get; private set; }

        public bool IsConfigured => true;

        public Task<MobileVerificationStart> StartAsync(Guid userId, string mobileNumber, CancellationToken cancellationToken)
        {
            (SentFor, SentTo) = (userId, mobileNumber);
            return Task.FromResult(new MobileVerificationStart($"challenge-{userId}", DateTimeOffset.UtcNow.AddMinutes(5)));
        }

        public Task<MobileVerificationOutcome> CompleteAsync(Guid userId, string mobileNumber, string challengeId, string code, CancellationToken cancellationToken) =>
            Task.FromResult(userId == SentFor && mobileNumber == SentTo && challengeId == $"challenge-{userId}" && code == Code
                ? MobileVerificationOutcome.Verified
                : MobileVerificationOutcome.Rejected);
    }
}
