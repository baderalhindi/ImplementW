using System.Net;
using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>Review round 5, item 5: finding a person for a field, for those who may write something, without USER_VIEW and without contact details.</summary>
[Collection(IdentitySuite.Name)]
public sealed class PersonLookupTests(IdentityTestHost host)
{
    private const string Lookups = "/api/v1/person-lookups";

    [Fact]
    public async Task APersonWhoMayWriteFindsPeopleByNameAndSeesNoContactDetails()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(3)).AccessToken;

        using HttpResponseMessage response = await client.GetAsync($"{Lookups}?q=local.r02", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonObject page = await response.ReadObjectAsync();
        JsonObject person = Assert.Single(page["items"]!.AsArray())!.AsObject();
        Assert.Equal("local.r02", person["username"]!.GetValue<string>());
        Assert.False(person.ContainsKey("email"));
    }

    [Fact]
    public async Task AnExternalUserIsHeldToItsOwnEntity()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(8)).AccessToken;

        using HttpResponseMessage response = await client.GetAsync($"{Lookups}?q=local.r02&userType=INTERNAL", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.ReadObjectAsync())["items"]!.AsArray());
    }

    /// <summary>
    /// QA D-006: a project the caller may not view is answered as one that does not exist, so its id is not confirmed and its role holders are not named.
    /// User 8 views its entity's project; the department's own project is not its to see.
    /// </summary>
    [Fact]
    public async Task AProjectTheCallerMayNotViewIsAnsweredAsOneThatDoesNotExist()
    {
        const string hiddenProjectId = "00000000-0150-4000-8000-000000000606";
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, title, title_lang, classification_item_id, department_id, external_entity_id, lifecycle_state,
                                         governance_profile_item_id, participation_mode, created_at, created_by, updated_at, updated_by)
            SELECT '{hiddenProjectId}', 'Department project', 'en', classification_item_id, department_id, NULL, 'SUBMITTED', governance_profile_item_id,
                   'AHDA_MANAGED', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM project.project WHERE id = '{IdentityDatabase.EntityProjectId}'
            ON CONFLICT (id) DO NOTHING;
            """);
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(8)).AccessToken;

        using HttpResponseMessage visible = await client.GetAsync($"{Lookups}?projectId={IdentityDatabase.EntityProjectId}", token);
        using HttpResponseMessage hidden = await client.GetAsync($"{Lookups}?projectId={hiddenProjectId}", token);
        using HttpResponseMessage unknown = await client.GetAsync($"{Lookups}?projectId={Guid.NewGuid()}", token);

        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
        string unknownBody = Problem(await unknown.ReadObjectAsync());
        Assert.Equal((HttpStatusCode.BadRequest, unknownBody), (hidden.StatusCode, Problem(await hidden.ReadObjectAsync())));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("projectId", unknownBody, StringComparison.Ordinal);
    }

    /// <summary>A problem's code and field errors: what tells one refusal from another, without its trace id.</summary>
    private static string Problem(JsonObject problem) => $"{problem["code"]} {problem["errors"]?.ToJsonString()}";

    [Fact]
    public async Task SomeoneWithNoWritePermissionIsRefused()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(5)).AccessToken;

        using HttpResponseMessage response = await client.GetAsync($"{Lookups}?q=local", token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
