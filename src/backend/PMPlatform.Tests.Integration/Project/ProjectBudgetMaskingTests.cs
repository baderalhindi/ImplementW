using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// shipped-permission-matrix.md D-4 on the project itself (stage QA PHASE-C-01): the registration budget is a SAR amount, so a caller without
/// FINANCIAL_VIEW_SENSITIVE on the project gets it omitted and named in <c>maskedFields</c>, as the financial screens and dashboards withhold
/// theirs. The Viewer (R06, DEPT) and the entity (R08, ENTITY) hold none; the Portfolio Manager (R02, ALL) and the project's own Project
/// Manager (R04, OWN) hold it.
/// </summary>
[Collection(ProjectSuite.Name)]
public sealed class ProjectBudgetMaskingTests(ProjectTestHost host)
{
    private const string Budget = "registrationBudgetSar";

    [Fact]
    public async Task TheBudgetIsWithheldFromTheViewerAndTheEntityAndShownToThePortfolioManagerAndTheProjectsManager()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;
        string manager = (await client.SignInOrFailAsync(5)).AccessToken;

        // Registered by local.r03 for the entity, so local.r08 reaches it through R08 and does not own it; local.r05 manages it.
        Guid projectId = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Reviewer, host.Registration()));
        await client.CommandOrFailAsync(sessions.Reviewer, projectId, "submit", new { projectManagerUserId = ProjectDriver.Person(5) });

        foreach (string withheld in new[] { viewer, sessions.Entity })
        {
            JsonObject project = await ReadAsync(client, withheld, projectId);
            Assert.False(project.ContainsKey(Budget), project.ToJsonString());
            Assert.Equal([Budget], MaskedFields(project));
        }

        foreach (string revealed in new[] { sessions.Approver, manager })
        {
            JsonObject project = await ReadAsync(client, revealed, projectId);
            Assert.Equal("12500000.00", project[Budget]!.GetValue<string>());
            Assert.Empty(MaskedFields(project));
        }
    }

    /// <summary>The entity is never sent the budget, so its form sends none back: saving its draft keeps the budget rather than clearing it.</summary>
    [Fact]
    public async Task AnEntitySavingADraftWithoutTheWithheldBudgetKeepsIt()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Reviewer, host.Registration()));
        string path = $"{ProjectDriver.Projects}/{projectId}";

        string eTag;
        using (HttpResponseMessage read = await client.GetAsync(path, sessions.Entity))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            eTag = AdministrationApi.ETagOf(read);
        }

        using (HttpResponseMessage saved = await client.PutAsync(path, sessions.Entity, host.Registration(change: r =>
               {
                   r[Budget] = null;
                   r["title"]!["text"] = "Regional road upgrade, phase 2";
               }), eTag))
        {
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            JsonObject project = await saved.ReadObjectAsync();
            Assert.False(project.ContainsKey(Budget), project.ToJsonString());
            Assert.Equal([Budget], MaskedFields(project));
        }

        Assert.Equal("12500000.00", (await ReadAsync(client, sessions.Approver, projectId))[Budget]!.GetValue<string>());
    }

    private static async Task<JsonObject> ReadAsync(HttpClient client, string token, Guid projectId)
    {
        using HttpResponseMessage read = await client.GetAsync($"{ProjectDriver.Projects}/{projectId}", token);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        return await read.ReadObjectAsync();
    }

    private static string[] MaskedFields(JsonObject project) => [.. project["maskedFields"]!.AsArray().Select(f => f!.GetValue<string>())];
}
