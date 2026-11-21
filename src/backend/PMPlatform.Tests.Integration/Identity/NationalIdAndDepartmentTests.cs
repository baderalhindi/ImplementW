using System.Net;
using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// Client review: the National ID or Iqama number, sealed at rest, masked on screen and read in full only by USER_NATIONAL_ID_VIEW (item 7);
/// and deleting a department, refused while anything depends on it (item 6).
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class NationalIdAndDepartmentTests(IdentityTestHost host)
{
    private const string Users = AdministrationApi.Users;
    private const string ValidId = "1000000008";
    private const string OtherValidId = "2000000006";

    [Fact]
    public async Task ANationalIdIsSealedMaskedAndRevealedOnlyToItsHolderOfThePermissionWithAnAuditEvent()
    {
        using HttpClient client = host.Api.CreateClient();
        string administrator = (await client.SignInOrFailAsync(1)).AccessToken;
        string person = IdentityDatabase.UserId(5);
        try
        {
            using (HttpResponseMessage set = await client.PutAsync($"{Users}/{person}/national-id", administrator, new { nationalId = ValidId }, ifMatch: null))
            {
                Assert.Equal(HttpStatusCode.OK, set.StatusCode);
                Assert.Equal("******0008", (await set.ReadObjectAsync())["nationalIdMasked"]!.GetValue<string>());
            }

            string stored = Assert.Single(await host.Database.QueryAsync(
                $"SELECT concat_ws('|', national_id_sealed, national_id_fingerprint, national_id_last_digits) FROM identity_access.\"user\" WHERE id = '{person}'"));
            Assert.DoesNotContain(ValidId, stored, StringComparison.Ordinal);
            Assert.StartsWith("v1.", stored, StringComparison.Ordinal);

            using (HttpResponseMessage read = await client.GetAsync($"{Users}/{person}", administrator))
            {
                string body = await read.Content.ReadAsStringAsync();
                Assert.DoesNotContain(ValidId, body, StringComparison.Ordinal);
            }

            using (HttpResponseMessage revealed = await client.GetAsync($"{Users}/{person}/national-id", administrator))
            {
                Assert.Equal(HttpStatusCode.OK, revealed.StatusCode);
                Assert.True(revealed.Headers.CacheControl?.NoStore);
                Assert.Equal(ValidId, (await revealed.ReadObjectAsync())["nationalId"]!.GetValue<string>());
            }

            Assert.Contains("IdentityAccess.UserNationalIdRevealed",
                await host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_id = '{person}'"));

            string viewer = (await client.SignInOrFailAsync(6)).AccessToken;
            using HttpResponseMessage refused = await client.GetAsync($"{Users}/{person}/national-id", viewer);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }
        finally
        {
            using HttpResponseMessage cleared = await client.PutAsync($"{Users}/{person}/national-id", administrator, new { nationalId = (string?)null }, ifMatch: null);
            Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
            Assert.Null((await cleared.ReadObjectAsync())["nationalIdMasked"]);
        }
    }

    [Fact]
    public async Task ANationalIdMustBeWellFormedAndBelongToOnePerson()
    {
        using HttpClient client = host.Api.CreateClient();
        string administrator = (await client.SignInOrFailAsync(1)).AccessToken;
        foreach (string malformed in new[] { "1000000009", "3000000002", "12345", "10000000O8" })
        {
            using HttpResponseMessage refused = await client.PutAsync($"{Users}/{IdentityDatabase.UserId(5)}/national-id", administrator, new { nationalId = malformed }, ifMatch: null);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }

        try
        {
            using (HttpResponseMessage first = await client.PutAsync($"{Users}/{IdentityDatabase.UserId(5)}/national-id", administrator, new { nationalId = OtherValidId }, ifMatch: null))
            {
                Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            }

            using HttpResponseMessage taken = await client.PutAsync($"{Users}/{IdentityDatabase.UserId(6)}/national-id", administrator, new { nationalId = OtherValidId }, ifMatch: null);
            Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        }
        finally
        {
            using HttpResponseMessage cleared = await client.PutAsync($"{Users}/{IdentityDatabase.UserId(5)}/national-id", administrator, new { nationalId = (string?)null }, ifMatch: null);
        }
    }

    [Fact]
    public async Task ADepartmentInUseIsNotDeletedAndAnUnusedOneLeavesAdministration()
    {
        using HttpClient client = host.Api.CreateClient();
        string administrator = (await client.SignInOrFailAsync(1)).AccessToken;

        using (HttpResponseMessage inUse = await client.PostAsync($"{AdministrationApi.Departments}/{IdentityDatabase.DepartmentId}/delete", administrator))
        {
            Assert.Equal(HttpStatusCode.Conflict, inUse.StatusCode);
            JsonObject problem = await inUse.ReadObjectAsync();
            Assert.Equal("IDENTITY_ACCESS_DEPARTMENT_IN_USE", problem["code"]!.GetValue<string>());
            Assert.Contains("users", problem["errors"]!.AsArray().Select(e => e!["field"]!.GetValue<string>()));
        }

        string code = $"DEL-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        using HttpResponseMessage created = await client.PostAsync(AdministrationApi.Departments, administrator, new
        {
            code,
            name = new { ar = "إدارة للحذف", en = "Department to delete" },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Guid id = AdministrationApi.IdOf(await created.ReadObjectAsync());

        using (HttpResponseMessage deleted = await client.PostAsync($"{AdministrationApi.Departments}/{id}/delete", administrator))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        using (HttpResponseMessage read = await client.GetAsync($"{AdministrationApi.Departments}/{id}", administrator))
        {
            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        }

        using (HttpResponseMessage list = await client.GetAsync($"{AdministrationApi.Departments}?q={code}", administrator))
        {
            Assert.Empty((await list.ReadObjectAsync())["items"]!.AsArray());
        }

        Assert.Equal(["f|Department to delete"], await host.Database.QueryAsync(
            $"SELECT CASE WHEN is_active THEN 't' ELSE 'f' END || '|' || name_en FROM identity_access.department WHERE id = '{id}' AND deleted_at IS NOT NULL"));
    }
}
