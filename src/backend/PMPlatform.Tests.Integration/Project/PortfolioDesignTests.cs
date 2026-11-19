using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// The Portfolio dashboard designer (the 17 Nov 2026 designer, dashboard-designer.md) through the API: local.r02 (R02, <c>DASHBOARD_DESIGN</c> at ALL)
/// reads the data catalogue, previews, saves and resets the one design everyone sees; local.r06 (R06, without it) is refused every design write and reads
/// the saved design; a design breaking a rule is refused with 422 <c>PROJECT_PORTFOLIO_DESIGN_INVALID</c> and saves nothing. A bar's axes (the 19 Nov 2026
/// axes) are saved and read back as set, titles trimmed; a chart drawn with the default axes stores none.
/// </summary>
[Collection(ProjectSuite.Name)]
public sealed class PortfolioDesignTests(ProjectTestHost host)
{
    private const string Design = "/api/v1/portfolio-insights/design";

    /// <summary>A bar's axes as the designer sends them: the category axis hidden with a title (padded, saved trimmed), the value axis from 0 to 50 without gridlines.</summary>
    private static readonly object StatusAxes = new
    {
        category = new { show = false, title = new { ar = " الحالة ", en = "Status  " } },
        value = new { show = true, title = (object?)null, min = 0, max = 50, gridlines = false },
    };

    [Fact]
    public async Task ThePortfolioManagerSavesTheDesignEveryoneReadsAndResetsIt()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(2)).AccessToken;
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;

        using HttpResponseMessage fieldsResponse = await client.GetAsync($"{Design}/fields", manager);
        Assert.Equal(HttpStatusCode.OK, fieldsResponse.StatusCode);
        JsonArray fields = (await fieldsResponse.ReadObjectAsync())["fields"]!.AsArray();
        JsonNode field = fields.First(f => f!["grain"]?.GetValue<string>() != "SNAPSHOT" && f["aggregations"]!.AsArray().Count > 0)!;
        string fieldKey = field["key"]!.GetValue<string>();
        string aggregation = field["aggregations"]![0]!.GetValue<string>();

        object design = DesignOf(
            Visual("Projects", "CARD", 0, 0, 3, 2, measures: ["PROJECTS"]),
            Visual("By status", "BAR", 3, 0, 6, 4, measures: ["PROJECTS"], groupBy: "STATUS", axes: StatusAxes),
            Visual("Field value", "CARD", 9, 0, 3, 2, values: [new { field = fieldKey, aggregation }]));

        using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", manager, design);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        JsonObject previewed = await preview.ReadObjectAsync();
        Assert.False(previewed["isStandard"]!.GetValue<bool>());
        Assert.Equal(3, previewed["visuals"]!.AsArray().Count);

        using HttpResponseMessage saved = await client.PutAsync(Design, manager, design, ifMatch: null);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        // The saved design is the one everyone reads, the viewer without DASHBOARD_DESIGN too, in the order and at the places saved.
        foreach (string reader in new[] { manager, viewer })
        {
            using HttpResponseMessage read = await client.GetAsync(Design, reader);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            JsonObject view = await read.ReadObjectAsync();
            Assert.False(view["isStandard"]!.GetValue<bool>());
            Assert.NotNull(view["updatedAt"]?.GetValue<DateTimeOffset>());
            JsonArray visuals = view["visuals"]!.AsArray();
            Assert.Equal(["Projects", "By status", "Field value"], visuals.Select(v => v!["title"]!["en"]!.GetValue<string>()));
            Assert.Equal(["CARD", "BAR", "CARD"], visuals.Select(v => v!["visualType"]!.GetValue<string>()));
            Assert.Equal("STATUS", visuals[1]!["groupBy"]!.GetValue<string>());
            Assert.Equal(9, visuals[2]!["layoutColumn"]!.GetValue<int>());
            Assert.Equal(fieldKey, visuals[2]!["values"]![0]!["field"]!.GetValue<string>());
            JsonNode axes = visuals[1]!["axes"]!;
            Assert.False(axes["category"]!["show"]!.GetValue<bool>());
            Assert.Equal(("الحالة", "Status"), (axes["category"]!["title"]!["ar"]!.GetValue<string>(), axes["category"]!["title"]!["en"]!.GetValue<string>()));
            Assert.True(axes["value"]!["show"]!.GetValue<bool>());
            Assert.Null(axes["value"]!["title"]);
            Assert.Equal((0m, 50m), (axes["value"]!["min"]!.GetValue<decimal>(), axes["value"]!["max"]!.GetValue<decimal>()));
            Assert.False(axes["value"]!["gridlines"]!.GetValue<bool>());
            Assert.Null(visuals[0]!["axes"]);
        }

        Assert.Equal(["3"], await host.Database.QueryAsync("SELECT count(*)::text FROM project.portfolio_visual"));
        Assert.Equal(["CARD:", "BAR:Status", "CARD:"], await host.Database.QueryAsync(
            "SELECT visual_type || ':' || coalesce(axes -> 'category' -> 'title' ->> 'en', '') FROM project.portfolio_visual ORDER BY sort_order"));

        using HttpResponseMessage reset = await client.PostAsync($"{Design}/reset", manager);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using HttpResponseMessage standard = await client.GetAsync(Design, manager);
        Assert.True((await standard.ReadObjectAsync())["isStandard"]!.GetValue<bool>());
        Assert.Equal(["0"], await host.Database.QueryAsync("SELECT count(*)::text FROM project.portfolio_visual"));
    }

    [Fact]
    public async Task ARoleWithoutDashboardDesignIsRefusedEveryDesignWrite()
    {
        using HttpClient client = host.Api.CreateClient();
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;
        object design = DesignOf(Visual("Projects", "CARD", 0, 0, 3, 2, measures: ["PROJECTS"]));

        using HttpResponseMessage fields = await client.GetAsync($"{Design}/fields", viewer);
        using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", viewer, design);
        using HttpResponseMessage save = await client.PutAsync(Design, viewer, design, ifMatch: null);
        using HttpResponseMessage reset = await client.PostAsync($"{Design}/reset", viewer);

        Assert.Equal(
            [HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden],
            new[] { fields.StatusCode, preview.StatusCode, save.StatusCode, reset.StatusCode });
        Assert.Equal(["0"], await host.Database.QueryAsync("SELECT count(*)::text FROM project.portfolio_visual"));
    }

    [Fact]
    public async Task ADesignBreakingARuleIsRefusedAndSavesNothing()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(2)).AccessToken;

        // Two visuals on the same grid cell, and a card grouped though it shows one figure.
        object overlapping = DesignOf(
            Visual("First", "CARD", 0, 0, 3, 2, measures: ["PROJECTS"]),
            Visual("Second", "CARD", 2, 1, 3, 2, measures: ["IN_PROGRESS"]));
        object groupedCard = DesignOf(Visual("Grouped card", "CARD", 0, 0, 3, 2, measures: ["PROJECTS"], groupBy: "STATUS"));

        // A value axis ending where it starts, and axes on a card.
        object emptyRange = DesignOf(Visual(
            "Empty range", "COLUMN", 0, 0, 6, 4, measures: ["PROJECTS"], groupBy: "STATUS",
            axes: new { category = new { show = true }, value = new { show = true, min = 10, max = 10, gridlines = true } }));
        object cardAxes = DesignOf(Visual("Card axes", "CARD", 0, 0, 3, 2, measures: ["PROJECTS"], axes: StatusAxes));

        foreach (object design in new[] { overlapping, groupedCard, emptyRange, cardAxes })
        {
            using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", manager, design);
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_PORTFOLIO_DESIGN_INVALID"), (preview.StatusCode, await preview.CodeOfAsync()));
            using HttpResponseMessage save = await client.PutAsync(Design, manager, design, ifMatch: null);
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_PORTFOLIO_DESIGN_INVALID"), (save.StatusCode, await save.CodeOfAsync()));
        }

        Assert.Equal(["0"], await host.Database.QueryAsync("SELECT count(*)::text FROM project.portfolio_visual"));
    }

    private static object DesignOf(params object[] visuals) => new { visuals };

    private static object Visual(
        string title, string type, short column, short row, short width, short height, string[]? measures = null, string groupBy = "NONE", object[]? values = null,
        object? axes = null) =>
        new
        {
            title = new { ar = $"عنوان {title}", en = title },
            visualType = type,
            measures,
            groupBy,
            layoutColumn = column,
            layoutRow = row,
            layoutWidth = width,
            layoutHeight = height,
            values,
            axes,
        };
}
