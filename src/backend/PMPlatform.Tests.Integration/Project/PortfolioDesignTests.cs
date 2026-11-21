using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>
/// The Portfolio dashboard designer (the 17 Nov 2026 designer, dashboard-designer.md) through the API: local.r02 (R02, <c>DASHBOARD_DESIGN</c> at ALL)
/// reads the data catalogue, previews, saves and resets the one design everyone sees; local.r06 (R06, without it) is refused every design write and reads
/// the saved design; a design breaking a rule is refused with 422 <c>PROJECT_PORTFOLIO_DESIGN_INVALID</c> and saves nothing. A bar's axes (the 19 Nov 2026
/// axes) are saved and read back as set, titles trimmed; a chart drawn with the default axes stores none. Two DASHBOARD_WIDGET sections (the 19 Nov 2026
/// widgets) are saved and read back with their widget codes; two naming the same widget are refused. A container (the 19 Nov 2026 containers) is saved with
/// its children and read back with them nested in order, each with its figures; a child covering a sibling or outside its container is refused. A design
/// with decimal places and sizes (the 20 Nov 2026 decimal layout) is saved and read back exactly; one with two decimal places is refused. A table section's
/// hidden columns (the 20 Nov 2026 table columns) are saved and read back; a card hiding columns is refused. A TABLE of columns (the 20 Nov 2026 Power
/// BI-style table) previews, saves and reads back with its table of figures; a card with columns is refused. A container of a card per box of the on-plan
/// indicator and the follow-up reasons (the 20 Nov 2026 box sections) previews, saves and reads back each box's count as GET /portfolio-insights has it. A
/// TEXT visual (the 20 Nov 2026 text box), on the dashboard and in a container, previews, saves and reads back its words exactly, line breaks kept; words on
/// a card, a text box without words or in the wrong language are refused and save nothing. Two MAP visuals (the 20 Nov 2026 map visual) with different
/// filters, one in a container with its options set, preview, save and read back with their groups per city — an approved project in the test city among
/// them — each a bar's by city with the same filters, and none for local.r06, who lacks the city grouping's permission; map options on a card, a map grouped by sector or drawing an average are refused and save nothing.
/// Visuals of risks (the 20 Nov 2026 record sources) over two risks of an approved project — the number of risks, a bar by status and a table of their titles —
/// preview, save and read back, local.r02 holding RISK_VIEW at ALL; the risk fields are listed; a gauge of risks is refused and saves nothing.
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
            Visual("Field value", "CARD", 9, 0, 3, 2, values: [new { field = fieldKey, aggregation }]),
            Widget("Open risks by rating", "OPEN_RISKS_BY_RATING", 0, 4, 6, 7),
            Widget("Projects by lifecycle state", "PROJECTS_BY_LIFECYCLE_STATE", 6, 4, 6, 7));

        using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", manager, design);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        JsonObject previewed = await preview.ReadObjectAsync();
        Assert.False(previewed["isStandard"]!.GetValue<bool>());
        Assert.Equal(5, previewed["visuals"]!.AsArray().Count);
        Assert.Equal(
            [null, null, null, "OPEN_RISKS_BY_RATING", "PROJECTS_BY_LIFECYCLE_STATE"],
            previewed["visuals"]!.AsArray().Select(v => v!["widgetCode"]?.GetValue<string>()));

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
            Assert.Equal(
                ["Projects", "By status", "Field value", "Open risks by rating", "Projects by lifecycle state"], visuals.Select(v => v!["title"]!["en"]!.GetValue<string>()));
            Assert.Equal(["CARD", "BAR", "CARD", "SECTION", "SECTION"], visuals.Select(v => v!["visualType"]!.GetValue<string>()));
            Assert.Equal(
                [null, null, null, "DASHBOARD_WIDGET:OPEN_RISKS_BY_RATING", "DASHBOARD_WIDGET:PROJECTS_BY_LIFECYCLE_STATE"],
                visuals.Select(v => v!["section"] is { } section ? $"{section.GetValue<string>()}:{v["widgetCode"]!.GetValue<string>()}" : null));
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

        Assert.Equal(["5"], await host.Database.QueryAsync("SELECT count(*)::text FROM project.portfolio_visual"));
        Assert.Equal(["CARD::", "BAR:Status:", "CARD::", "SECTION::OPEN_RISKS_BY_RATING", "SECTION::PROJECTS_BY_LIFECYCLE_STATE"], await host.Database.QueryAsync(
            "SELECT visual_type || ':' || coalesce(axes -> 'category' -> 'title' ->> 'en', '') || ':' || coalesce(widget_code, '') FROM project.portfolio_visual ORDER BY sort_order"));

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

        // Two visuals drawing the same governed widget.
        object sameWidget = DesignOf(Widget("Risks", "OPEN_RISKS_BY_RATING", 0, 0, 6, 7), Widget("Risks again", "OPEN_RISKS_BY_RATING", 6, 0, 6, 7));
        using (HttpResponseMessage duplicate = await client.PostAsync($"{Design}/preview", manager, sameWidget))
        {
            JsonNode error = (await duplicate.ReadObjectAsync())["errors"]![0]!;
            Assert.Equal(("visuals[1].widgetCode", "DUPLICATE"), (error["field"]!.GetValue<string>(), error["code"]!.GetValue<string>()));
        }

        // A card hiding table columns (the 20 Nov 2026 table columns): only a table section has columns to hide.
        object cardColumns = DesignOf(Visual("Card columns", "CARD", 0, 0, 3, 2, measures: ["PROJECTS"], hiddenColumns: ["VALUE"]));
        using (HttpResponseMessage notAllowed = await client.PostAsync($"{Design}/preview", manager, cardColumns))
        {
            JsonNode error = (await notAllowed.ReadObjectAsync())["errors"]![0]!;
            Assert.Equal(("visuals[0].hiddenColumns", "NOT_ALLOWED"), (error["field"]!.GetValue<string>(), error["code"]!.GetValue<string>()));
        }

        foreach (object design in new[] { overlapping, groupedCard, emptyRange, cardAxes, sameWidget, cardColumns })
        {
            using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", manager, design);
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_PORTFOLIO_DESIGN_INVALID"), (preview.StatusCode, await preview.CodeOfAsync()));
            using HttpResponseMessage save = await client.PutAsync(Design, manager, design, ifMatch: null);
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_PORTFOLIO_DESIGN_INVALID"), (save.StatusCode, await save.CodeOfAsync()));
        }

        Assert.Equal(["0"], await host.Database.QueryAsync("SELECT count(*)::text FROM project.portfolio_visual"));
    }

    [Fact]
    public async Task AContainerIsSavedWithItsChildrenAndReadBackNestedInOrder()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(2)).AccessToken;
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;

        object design = DesignOf(
            Visual("Overview", "CONTAINER", 0, 0, 12, 7, children:
            [
                Visual("Projects", "CARD", 0, 0, 4, 2, measures: ["PROJECTS"]),
                Visual("By status", "BAR", 4, 0, 8, 6, measures: ["PROJECTS"], groupBy: "STATUS"),
            ]),
            Visual("Below", "CARD", 0, 7, 4, 2, measures: ["IN_PROGRESS"]));

        using HttpResponseMessage saved = await client.PutAsync(Design, manager, design, ifMatch: null);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        AssertNested((await saved.ReadObjectAsync())["visuals"]!.AsArray(), figures: true);

        foreach (string reader in new[] { manager, viewer })
        {
            using HttpResponseMessage read = await client.GetAsync(Design, reader);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            AssertNested((await read.ReadObjectAsync())["visuals"]!.AsArray(), figures: reader == manager);
        }

        // The children are rows of their own, pointing at their container, after it in the reading order.
        Assert.Equal(["1:CONTAINER:", "2:CARD:Overview", "3:BAR:Overview", "4:CARD:"], await host.Database.QueryAsync(
            "SELECT v.sort_order || ':' || v.visual_type || ':' || coalesce(p.title_en, '') FROM project.portfolio_visual v "
            + "LEFT JOIN project.portfolio_visual p ON p.id = v.parent_id ORDER BY v.sort_order"));

        using HttpResponseMessage reset = await client.PostAsync($"{Design}/reset", manager);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(["0"], await host.Database.QueryAsync("SELECT count(*)::text FROM project.portfolio_visual"));

        // Each child carries its figures as a top-level visual does; the viewer's depend on the insights permissions R06 holds, so only the manager's are checked.
        static void AssertNested(JsonArray visuals, bool figures)
        {
            Assert.Equal(["Overview", "Below"], visuals.Select(v => v!["title"]!["en"]!.GetValue<string>()));
            Assert.Equal("CONTAINER", visuals[0]!["visualType"]!.GetValue<string>());
            Assert.Empty(visuals[0]!["groups"]!.AsArray());
            Assert.Null(visuals[1]!["children"]);
            JsonArray children = visuals[0]!["children"]!.AsArray();
            Assert.Equal(["Projects", "By status"], children.Select(c => c!["title"]!["en"]!.GetValue<string>()));
            Assert.Equal((4, 0, 8, 6), (Int(children[1]!, "layoutColumn"), Int(children[1]!, "layoutRow"), Int(children[1]!, "layoutWidth"), Int(children[1]!, "layoutHeight")));
            if (figures)
            {
                Assert.Single(children[0]!["groups"]!.AsArray());
                Assert.NotNull(children[1]!["groups"]!.AsArray());
            }

            Assert.All(children, c => Assert.Null(c!["children"]));
        }
    }

    [Fact]
    public async Task AChildCoveringASiblingOrOutsideItsContainerIsRefusedAndSavesNothing()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(2)).AccessToken;

        object overlapping = DesignOf(Visual("Box", "CONTAINER", 0, 0, 12, 6, children:
        [
            Visual("First", "CARD", 0, 0, 4, 2, measures: ["PROJECTS"]),
            Visual("Second", "CARD", 3, 1, 4, 2, measures: ["IN_PROGRESS"]),
        ]));
        object outside = DesignOf(Visual("Box", "CONTAINER", 0, 0, 6, 6, children: [Visual("Too wide", "CARD", 4, 0, 4, 2, measures: ["PROJECTS"])]));
        object belowTheBox = DesignOf(Visual("Box", "CONTAINER", 0, 0, 12, 4, children: [Visual("Too low", "CARD", 0, 2, 4, 2, measures: ["PROJECTS"])]));

        foreach ((object design, string field, string code) in new[]
        {
            (overlapping, "visuals[0].children[1].layout", "IN_USE"),
            (outside, "visuals[0].children[0].layout", "OUTSIDE_RANGE"),
            (belowTheBox, "visuals[0].children[0].layout", "OUTSIDE_RANGE"),
        })
        {
            using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", manager, design);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, preview.StatusCode);
            JsonObject body = await preview.ReadObjectAsync();
            Assert.Equal("PROJECT_PORTFOLIO_DESIGN_INVALID", body["code"]!.GetValue<string>());
            Assert.Equal((field, code), (body["errors"]![0]!["field"]!.GetValue<string>(), body["errors"]![0]!["code"]!.GetValue<string>()));
            using HttpResponseMessage save = await client.PutAsync(Design, manager, design, ifMatch: null);
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_PORTFOLIO_DESIGN_INVALID"), (save.StatusCode, await save.CodeOfAsync()));
        }

        Assert.Equal(["0"], await host.Database.QueryAsync("SELECT count(*)::text FROM project.portfolio_visual"));
    }

    [Fact]
    public async Task ADesignWithDecimalPlacesIsSavedAndReadBackExactly()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(2)).AccessToken;

        object design = DesignOf(
            Visual("Left", "CARD", 0, 0, 4.5m, 2, measures: ["PROJECTS"]),
            Visual("Right", "CARD", 4.5m, 0, 4.7m, 2.3m, measures: ["IN_PROGRESS"]),
            Visual("Box", "CONTAINER", 0, 2.3m, 4.5m, 3.5m, children: [Visual("Inside", "CARD", 0, 0.5m, 4.5m, 2, measures: ["PROJECTS"])]));
        string[] expected = ["0,0,4.5,2", "4.5,0,4.7,2.3", "0,2.3,4.5,3.5"];

        using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", manager, design);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(expected, PlacesOf(await preview.ReadObjectAsync()));

        using HttpResponseMessage saved = await client.PutAsync(Design, manager, design, ifMatch: null);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(expected, PlacesOf(await saved.ReadObjectAsync()));

        using HttpResponseMessage read = await client.GetAsync(Design, manager);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        JsonObject body = await read.ReadObjectAsync();
        Assert.Equal(expected, PlacesOf(body));
        Assert.Equal("0,0.5,4.5,2", Place(body["visuals"]![2]!["children"]![0]!));
        Assert.Equal(["1:0.0,0.0,4.5,2.0", "2:4.5,0.0,4.7,2.3", "3:0.0,2.3,4.5,3.5", "4:0.0,0.5,4.5,2.0"], await host.Database.QueryAsync(
            "SELECT sort_order || ':' || layout_column || ',' || layout_row || ',' || layout_width || ',' || layout_height FROM project.portfolio_visual ORDER BY sort_order"));

        object twoDecimals = DesignOf(Visual("Left", "CARD", 0, 0, 4.55m, 2, measures: ["PROJECTS"]));
        using HttpResponseMessage refused = await client.PutAsync(Design, manager, twoDecimals, ifMatch: null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        JsonObject error = await refused.ReadObjectAsync();
        Assert.Equal(("PROJECT_PORTFOLIO_DESIGN_INVALID", "visuals[0].layout", "OUTSIDE_RANGE"), (error["code"]!.GetValue<string>(),
            error["errors"]![0]!["field"]!.GetValue<string>(), error["errors"]![0]!["code"]!.GetValue<string>()));

        using HttpResponseMessage reset = await client.PostAsync($"{Design}/reset", manager);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        // The numbers as the JSON carries them, so 4.5 is told from 4.50 and 2 from 2.0.
        static string[] PlacesOf(JsonObject view) => [.. view["visuals"]!.AsArray().Select(v => Place(v!))];

        static string Place(JsonNode v) =>
            string.Join(',', v["layoutColumn"]!.ToJsonString(), v["layoutRow"]!.ToJsonString(), v["layoutWidth"]!.ToJsonString(), v["layoutHeight"]!.ToJsonString());
    }

    [Fact]
    public async Task ATableSectionsHiddenColumnsAreSavedAndReadBack()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(2)).AccessToken;

        object design = DesignOf(
            Visual("By sector", "SECTION", 0, 0, 12, 8, section: "SECTOR_TABLE", hiddenColumns: ["VALUE", "GAP"]),
            Visual("By portfolio", "SECTION", 0, 8, 12, 6, section: "PORTFOLIO_TABLE", hiddenColumns: []),
            Visual("Card", "CARD", 0, 14, 4, 2, measures: ["PROJECTS"]));
        string?[] expected = ["VALUE,GAP", null, null];

        using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", manager, design);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(expected, HiddenOf(await preview.ReadObjectAsync()));

        using HttpResponseMessage saved = await client.PutAsync(Design, manager, design, ifMatch: null);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(expected, HiddenOf(await saved.ReadObjectAsync()));

        using HttpResponseMessage read = await client.GetAsync(Design, manager);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(expected, HiddenOf(await read.ReadObjectAsync()));
        // An empty list is stored as null: the section shows every column.
        Assert.Equal(["1:{VALUE,GAP}", "2:", "3:"], await host.Database.QueryAsync(
            "SELECT sort_order || ':' || COALESCE(hidden_columns::text, '') FROM project.portfolio_visual ORDER BY sort_order"));

        using HttpResponseMessage reset = await client.PostAsync($"{Design}/reset", manager);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        static string?[] HiddenOf(JsonObject view) =>
            [.. view["visuals"]!.AsArray().Select(v => v!["hiddenColumns"] is JsonArray codes ? string.Join(',', codes.Select(c => c!.GetValue<string>())) : null)];
    }

    [Fact]
    public async Task AColumnTableIsSavedAndReadBackWithItsTable()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(2)).AccessToken;
        object[] columns =
        [
            new { kind = "DIMENSION", key = "SECTOR", aggregation = "NONE" },
            new { kind = "MEASURE", key = "PROJECTS", aggregation = (string?)null },
            new { kind = "MEASURE", key = "VALUE_SAR", aggregation = (string?)null },
        ];
        object design = DesignOf(Visual("By sector", "TABLE", 0, 0, 12, 6, measures: [], columns: columns));

        // The rows are the insights' sector breakdown: the same sectors with the same projects.
        using HttpResponseMessage insights = await client.GetAsync("/api/v1/portfolio-insights", manager);
        Assert.Equal(HttpStatusCode.OK, insights.StatusCode);
        string[] bySector = [.. (await insights.ReadObjectAsync())["bySector"]!["rows"]!.AsArray()
            .Select(r => $"{r!["itemId"]!.GetValue<string>()}:{r["projects"]!.GetValue<int>()}").Order(StringComparer.Ordinal)];

        using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", manager, design);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        AssertTable(await preview.ReadObjectAsync(), bySector);

        using HttpResponseMessage saved = await client.PutAsync(Design, manager, design, ifMatch: null);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        AssertTable(await saved.ReadObjectAsync(), bySector);

        using HttpResponseMessage read = await client.GetAsync(Design, manager);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        AssertTable(await read.ReadObjectAsync(), bySector);
        Assert.Equal(["TABLE:{}:SECTOR,PROJECTS,VALUE_SAR"], await host.Database.QueryAsync(
            "SELECT visual_type || ':' || measures::text || ':' || (SELECT string_agg(c ->> 'key', ',') FROM jsonb_array_elements(data_fields -> 'columns') c) FROM project.portfolio_visual"));

        // Columns on anything but a table are refused, and nothing is saved.
        object card = DesignOf(Visual("Card", "CARD", 0, 0, 4, 2, measures: ["PROJECTS"], columns: columns));
        using HttpResponseMessage refused = await client.PutAsync(Design, manager, card, ifMatch: null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        JsonObject error = await refused.ReadObjectAsync();
        Assert.Equal(("PROJECT_PORTFOLIO_DESIGN_INVALID", "visuals[0].columns", "NOT_ALLOWED"), (error["code"]!.GetValue<string>(),
            error["errors"]![0]!["field"]!.GetValue<string>(), error["errors"]![0]!["code"]!.GetValue<string>()));
        Assert.Equal(["1"], await host.Database.QueryAsync("SELECT count(*)::text FROM project.portfolio_visual"));

        using HttpResponseMessage reset = await client.PostAsync($"{Design}/reset", manager);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        // The table: a type per column, a row per sector, its projects counted in the row and in the totals.
        static void AssertTable(JsonObject view, string[] bySector)
        {
            JsonNode visual = view["visuals"]![0]!;
            Assert.Equal(["SECTOR", "PROJECTS", "VALUE_SAR"], visual["columns"]!.AsArray().Select(c => c!["key"]!.GetValue<string>()));
            Assert.Empty(visual["groups"]!.AsArray());
            JsonNode table = visual["table"]!;
            Assert.Equal(["ITEM", "COUNT", "SAR"], table["columnTypes"]!.AsArray().Select(t => t!.GetValue<string>()));
            JsonArray rows = table["rows"]!.AsArray();
            Assert.Equal(bySector, rows.Select(r => $"{r!["cells"]![0]!["key"]!.GetValue<string>()}:{r["projects"]!.GetValue<int>()}").Order(StringComparer.Ordinal));
            Assert.All(rows, row => Assert.Equal(row!["projects"]!.GetValue<int>(), row["cells"]![1]!["value"]!.GetValue<int>()));
            Assert.Equal(rows.Sum(r => r!["projects"]!.GetValue<int>()), table["totals"]![1]!["value"]!.GetValue<int>());
            Assert.False(table["truncated"]!.GetValue<bool>());
        }
    }

    /// <summary>The box sections' figures and where GET /portfolio-insights has the same count.</summary>
    private static readonly (string Measure, string Section, string Box)[] Boxes =
    [
        ("ON_TRACK", "onPlan", "onTrack"), ("LATE", "onPlan", "late"), ("STALLED", "onPlan", "stalled"), ("NOT_MEASURED", "onPlan", "notMeasured"),
        ("FOLLOW_UP_STALLED", "followUpReasons", "stalled"), ("FOLLOW_UP_SUSPENDED", "followUpReasons", "suspended"), ("FOLLOW_UP_LATE", "followUpReasons", "late"),
        ("FOLLOW_UP_ACTION_OVERDUE", "followUpReasons", "overdueAction"), ("FOLLOW_UP_OPEN_OBSTACLE", "followUpReasons", "openObstacle"),
        ("NEEDING_FOLLOW_UP", "followUpReasons", "total"),
    ];

    [Fact]
    public async Task ACardPerBoxCountsWhatTheBoxSectionsShow()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(2)).AccessToken;
        // A container of the ten cards, four on a row of its inner grid, as Make editable lays the sections out.
        object design = DesignOf(Visual("Boxes", "CONTAINER", 0, 0, 12, 9, measures: [], children: [.. Boxes.Select((b, i) =>
            Visual(b.Measure, "CARD", i % 4 * 3, i / 4 * 2.5m, 3, 2.5m, measures: [b.Measure]))]));

        using HttpResponseMessage insights = await client.GetAsync("/api/v1/portfolio-insights", manager);
        Assert.Equal(HttpStatusCode.OK, insights.StatusCode);
        JsonObject sections = await insights.ReadObjectAsync();
        int[] expected = [.. Boxes.Select(b => Int(sections[b.Section]!, b.Box))];
        Assert.Equal(Int(sections["followUpReasons"]!, "total"), expected[4..9].Sum());

        using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", manager, design);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(expected, CountsOf(await preview.ReadObjectAsync()));

        // The database's check takes the new codes: the design saves and reads back the same counts.
        using HttpResponseMessage saved = await client.PutAsync(Design, manager, design, ifMatch: null);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using HttpResponseMessage read = await client.GetAsync(Design, manager);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(expected, CountsOf(await read.ReadObjectAsync()));
        Assert.Equal([.. Boxes.Select(b => $"{{{b.Measure}}}")], await host.Database.QueryAsync(
            "SELECT measures::text FROM project.portfolio_visual WHERE visual_type = 'CARD' ORDER BY sort_order"));

        using HttpResponseMessage reset = await client.PostAsync($"{Design}/reset", manager);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        static int[] CountsOf(JsonObject view) =>
            [.. view["visuals"]![0]!["children"]!.AsArray().Select(card => card!["groups"]![0]!["values"]![0]!.GetValue<int>())];
    }

    [Fact]
    public async Task ATextBoxIsSavedAndReadBackExactly()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(2)).AccessToken;
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;

        var heading = new { ar = "عنوان اللوحة\nسطر ثانٍ", en = "Dashboard heading\nA second line", size = "HEADING", align = "CENTER", bold = true };
        var note = new { ar = "ملاحظة", en = "A note", size = "SMALL", align = "END", bold = false };
        object design = DesignOf(
            Visual("Heading", "TEXT", 0, 0, 6, 2, text: heading),
            Visual("Box", "CONTAINER", 0, 2, 12, 4, children: [Visual("Note", "TEXT", 0, 0, 6, 2, text: note), Visual("Projects", "CARD", 6, 0, 4, 2, measures: ["PROJECTS"])]));
        JsonNode expectedHeading = JsonNode.Parse("""{"ar":"عنوان اللوحة\nسطر ثانٍ","en":"Dashboard heading\nA second line","size":"HEADING","align":"CENTER","bold":true,"fontSize":null,"italic":false,"underline":false,"color":null,"background":"NONE","border":false,"borderColor":null,"verticalAlign":"TOP"}""")!;
        JsonNode expectedNote = JsonNode.Parse("""{"ar":"ملاحظة","en":"A note","size":"SMALL","align":"END","bold":false,"fontSize":null,"italic":false,"underline":false,"color":null,"background":"NONE","border":false,"borderColor":null,"verticalAlign":"TOP"}""")!;

        using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", manager, design);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        AssertText(await preview.ReadObjectAsync());

        using HttpResponseMessage saved = await client.PutAsync(Design, manager, design, ifMatch: null);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        AssertText(await saved.ReadObjectAsync());
        foreach (string reader in new[] { manager, viewer })
        {
            using HttpResponseMessage read = await client.GetAsync(Design, reader);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            AssertText(await read.ReadObjectAsync());
        }

        // Only the text boxes carry text_box; its line breaks are stored as line breaks.
        Assert.Equal(["1:TEXT:HEADING:true", "2:CONTAINER::false", "3:TEXT:SMALL:false", "4:CARD::false"], await host.Database.QueryAsync(
            "SELECT sort_order || ':' || visual_type || ':' || coalesce(text_box->>'size', '') || ':' || (strpos(coalesce(text_box->>'en', ''), E'\\n') > 0)::text "
            + "FROM project.portfolio_visual ORDER BY sort_order"));

        using HttpResponseMessage reset = await client.PostAsync($"{Design}/reset", manager);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        foreach ((object refused, HttpStatusCode status, string code, string field, string fieldCode) in new[]
        {
            (DesignOf(Visual("Projects", "CARD", 0, 0, 4, 2, measures: ["PROJECTS"], text: note)), HttpStatusCode.UnprocessableEntity,
                "PROJECT_PORTFOLIO_DESIGN_INVALID", "visuals[0].text", "NOT_ALLOWED"),
            (DesignOf(Visual("Empty", "TEXT", 0, 0, 6, 2)), HttpStatusCode.UnprocessableEntity, "PROJECT_PORTFOLIO_DESIGN_INVALID", "visuals[0].text", "REQUIRED"),
            (DesignOf(Visual("Long", "TEXT", 0, 0, 6, 2, text: note with { en = "A" + new string('x', 2000) })), HttpStatusCode.UnprocessableEntity,
                "PROJECT_PORTFOLIO_DESIGN_INVALID", "visuals[0].text.en", "OUTSIDE_RANGE"),
            (DesignOf(Visual("Swapped", "TEXT", 0, 0, 6, 2, text: note with { en = "ملاحظة" })), HttpStatusCode.BadRequest, "VALIDATION_FAILED",
                "visuals[0].text.en", "NOT_ENGLISH"),
            (DesignOf(Visual("Tab", "TEXT", 0, 0, 6, 2, text: note with { en = "A\tnote" })), HttpStatusCode.BadRequest, "VALIDATION_FAILED",
                "visuals[0].text.en", "INVALID_CHARACTER"),
        })
        {
            using HttpResponseMessage save = await client.PutAsync(Design, manager, refused, ifMatch: null);
            Assert.Equal(status, save.StatusCode);
            JsonObject body = await save.ReadObjectAsync();
            Assert.Equal((code, field, fieldCode), (body["code"]!.GetValue<string>(), body["errors"]![0]!["field"]!.GetValue<string>(),
                body["errors"]![0]!["code"]!.GetValue<string>()));
        }

        Assert.Equal(["0"], await host.Database.QueryAsync("SELECT count(*)::text FROM project.portfolio_visual"));

        void AssertText(JsonObject view)
        {
            JsonArray visuals = view["visuals"]!.AsArray();
            Assert.Equal("TEXT", visuals[0]!["visualType"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(expectedHeading, visuals[0]!["text"]));
            Assert.Empty(visuals[0]!["groups"]!.AsArray());
            Assert.Empty(visuals[0]!["measures"]!.AsArray());
            Assert.Null(visuals[1]!["text"]);
            JsonArray children = visuals[1]!["children"]!.AsArray();
            Assert.True(JsonNode.DeepEquals(expectedNote, children[0]!["text"]));
            Assert.Null(children[1]!["text"]);
        }
    }

    /// <summary>
    /// 20 Nov 2026 text formatting: a fully formatted text box previews, saves and reads back exactly (its colours upper-case); one saved before its formatting
    /// reads back with the defaults (no background); a colour not #RRGGBB, a font size outside 8–96 or not whole, a background not CARD, NONE or a colour are
    /// refused and save nothing.
    /// </summary>
    [Fact]
    public async Task AFormattedTextBoxIsSavedAndReadBackExactly()
    {
        using HttpClient client = host.Api.CreateClient();
        string manager = (await client.SignInOrFailAsync(2)).AccessToken;
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;

        var words = new { ar = "ملاحظة", en = "A note", size = "NORMAL", align = "START", bold = false };
        var formatted = new
        {
            ar = "عنوان\nسطر ثانٍ",
            en = "Heading\nA second line",
            size = "LARGE",
            align = "CENTER",
            bold = true,
            fontSize = 32,
            italic = true,
            underline = true,
            color = "#1f4e79",
            background = "#FFF2cc",
            border = true,
            borderColor = "#BF9000",
            verticalAlign = "BOTTOM",
        };
        object design = DesignOf(
            Visual("Formatted", "TEXT", 0, 0, 6, 2, text: formatted),
            Visual("Box", "CONTAINER", 0, 2, 12, 4, children: [Visual("Card", "TEXT", 0, 0, 6, 2, text: new
            {
                words.ar,
                words.en,
                words.size,
                words.align,
                words.bold,
                background = "CARD",
                verticalAlign = "MIDDLE",
            })]));
        JsonNode expected = JsonNode.Parse("""
            {"ar":"عنوان\nسطر ثانٍ","en":"Heading\nA second line","size":"LARGE","align":"CENTER","bold":true,"fontSize":32,"italic":true,"underline":true,
             "color":"#1F4E79","background":"#FFF2CC","border":true,"borderColor":"#BF9000","verticalAlign":"BOTTOM"}
            """)!;
        JsonNode expectedCard = JsonNode.Parse("""
            {"ar":"ملاحظة","en":"A note","size":"NORMAL","align":"START","bold":false,"fontSize":null,"italic":false,"underline":false,"color":null,
             "background":"CARD","border":false,"borderColor":null,"verticalAlign":"MIDDLE"}
            """)!;

        using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", manager, design);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        AssertFormatted(await preview.ReadObjectAsync());

        using HttpResponseMessage saved = await client.PutAsync(Design, manager, design, ifMatch: null);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        AssertFormatted(await saved.ReadObjectAsync());
        foreach (string reader in new[] { manager, viewer })
        {
            using HttpResponseMessage read = await client.GetAsync(Design, reader);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            AssertFormatted(await read.ReadObjectAsync());
        }

        Assert.Equal(["1:#1F4E79:#FFF2CC:32", "3::CARD:"], await host.Database.QueryAsync(
            "SELECT sort_order || ':' || coalesce(text_box->>'color', '') || ':' || (text_box->>'background') || ':' || coalesce(text_box->>'fontSize', '') "
            + "FROM project.portfolio_visual WHERE visual_type = 'TEXT' ORDER BY sort_order"));

        // A text box saved before its formatting: its text_box without the new properties reads back with the defaults.
        await host.Database.ExecuteAsync(
            "UPDATE project.portfolio_visual SET text_box = '{\"ar\":\"ملاحظة\",\"en\":\"A note\",\"size\":\"NORMAL\",\"align\":\"START\",\"bold\":false}'::jsonb "
            + "WHERE sort_order = 1");
        using (HttpResponseMessage before = await client.GetAsync(Design, viewer))
        {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
            JsonNode? text = (await before.ReadObjectAsync())["visuals"]![0]!["text"];
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""
                {"ar":"ملاحظة","en":"A note","size":"NORMAL","align":"START","bold":false,"fontSize":null,"italic":false,"underline":false,"color":null,
                 "background":"NONE","border":false,"borderColor":null,"verticalAlign":"TOP"}
                """), text));
        }

        using HttpResponseMessage reset = await client.PostAsync($"{Design}/reset", manager);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        foreach ((object text, string field, string code) in new (object, string, string)[]
        {
            (new { words.ar, words.en, words.size, words.align, words.bold, color = "red" }, "visuals[0].text.color", "NOT_ALLOWED"),
            (new { words.ar, words.en, words.size, words.align, words.bold, borderColor = "#12345" }, "visuals[0].text.borderColor", "NOT_ALLOWED"),
            (new { words.ar, words.en, words.size, words.align, words.bold, background = "white" }, "visuals[0].text.background", "NOT_ALLOWED"),
            (new { words.ar, words.en, words.size, words.align, words.bold, fontSize = 100 }, "visuals[0].text.fontSize", "OUTSIDE_RANGE"),
            (new { words.ar, words.en, words.size, words.align, words.bold, fontSize = 7 }, "visuals[0].text.fontSize", "OUTSIDE_RANGE"),
            (new { words.ar, words.en, words.size, words.align, words.bold, fontSize = 14.5m }, "visuals[0].text.fontSize", "OUTSIDE_RANGE"),
        })
        {
            using HttpResponseMessage save = await client.PutAsync(Design, manager, DesignOf(Visual("Refused", "TEXT", 0, 0, 6, 2, text: text)), ifMatch: null);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, save.StatusCode);
            JsonObject body = await save.ReadObjectAsync();
            Assert.Equal(("PROJECT_PORTFOLIO_DESIGN_INVALID", field, code), (body["code"]!.GetValue<string>(), body["errors"]![0]!["field"]!.GetValue<string>(),
                body["errors"]![0]!["code"]!.GetValue<string>()));
        }

        Assert.Equal(["0"], await host.Database.QueryAsync("SELECT count(*)::text FROM project.portfolio_visual"));

        void AssertFormatted(JsonObject view)
        {
            JsonArray visuals = view["visuals"]!.AsArray();
            Assert.True(JsonNode.DeepEquals(expected, visuals[0]!["text"]));
            Assert.True(JsonNode.DeepEquals(expectedCard, visuals[1]!["children"]![0]!["text"]));
        }
    }

    [Fact]
    public async Task TwoMapsWithDifferentFiltersAreSavedAndReadBackWithTheirGroups()
    {
        using HttpClient client = host.Api.CreateClient();
        // An approved project in the test city: the portfolio leaves out projects still registering.
        Sessions sessions = await client.SignInAsync();
        await host.DecideAndDeliverAsync(await host.UnderReviewAsync(client, sessions), ApprovalTaskDecision.Approve);
        string manager = sessions.Approver;
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;

        object[] inCity = [new { field = "PROJECT.CITY", values = new[] { ProjectTestHost.CityId.ToString() } }];
        object[] approved = [new { field = "PROJECT.STATUS", values = new[] { "APPROVED_PLANNED" } }];
        // Each map beside a bar by the same place with the same figure and filters: the map's groups are the bar's.
        object design = DesignOf(
            Visual("In the city", "MAP", 0, 0, 12, 7, measures: ["PROJECTS"], groupBy: "CITY", filters: inCity),
            Visual("In the city by city", "BAR", 0, 7, 6, 5, measures: ["PROJECTS"], groupBy: "CITY", filters: inCity),
            Visual("Approved by city", "BAR", 6, 7, 6, 5, measures: ["PROJECTS"], groupBy: "CITY", filters: approved),
            Visual("Box", "CONTAINER", 0, 12, 12, 8, children:
            [
                Visual("Approved projects", "MAP", 0, 0, 12, 7, measures: ["PROJECTS"], groupBy: "CITY", filters: approved, map: new { showList = false, showCounts = true }),
            ]));

        using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", manager, design);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        AssertMaps(await preview.ReadObjectAsync());

        using HttpResponseMessage saved = await client.PutAsync(Design, manager, design, ifMatch: null);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        AssertMaps(await saved.ReadObjectAsync());
        using (HttpResponseMessage read = await client.GetAsync(Design, manager))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            AssertMaps(await read.ReadObjectAsync());
        }

        // local.r06 reads the maps with their options and, without PORTFOLIO_BREAKDOWN_VIEW (the city grouping's permission), no groups, as the bars.
        using (HttpResponseMessage read = await client.GetAsync(Design, viewer))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            JsonArray visuals = (await read.ReadObjectAsync())["visuals"]!.AsArray();
            JsonNode?[] maps = [visuals[0], visuals[3]!["children"]![0]];
            Assert.Equal([true, false], maps.Select(m => m!["map"]!["showList"]!.GetValue<bool>()));
            Assert.All([.. maps, visuals[1], visuals[2]], v => Assert.Null(v!["groups"]));
        }

        // The options are stored in data_fields beside the filters; a map sent without options stores the defaults.
        Assert.Equal(["1:MAP:true:true:1", "5:MAP:false:true:1"], await host.Database.QueryAsync(
            "SELECT sort_order || ':' || visual_type || ':' || (data_fields->'map'->>'showList') || ':' || (data_fields->'map'->>'showCounts') || ':' "
            + "|| coalesce(jsonb_array_length(data_fields->'filters'), 0) FROM project.portfolio_visual WHERE visual_type = 'MAP' ORDER BY sort_order"));

        using HttpResponseMessage reset = await client.PostAsync($"{Design}/reset", manager);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        foreach ((object refused, string field) in new[]
        {
            (DesignOf(Visual("Projects", "CARD", 0, 0, 4, 2, measures: ["PROJECTS"], map: new { showList = true, showCounts = true })), "visuals[0].map"),
            (DesignOf(Visual("By sector", "MAP", 0, 0, 12, 7, measures: ["PROJECTS"], groupBy: "SECTOR")), "visuals[0].groupBy"),
            (DesignOf(Visual("Average", "MAP", 0, 0, 12, 7, measures: ["AVERAGE_ACTUAL"], groupBy: "CITY")), "visuals[0].measures"),
        })
        {
            using HttpResponseMessage save = await client.PutAsync(Design, manager, refused, ifMatch: null);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, save.StatusCode);
            JsonObject body = await save.ReadObjectAsync();
            Assert.Equal(("PROJECT_PORTFOLIO_DESIGN_INVALID", field, "NOT_ALLOWED"), (body["code"]!.GetValue<string>(), body["errors"]![0]!["field"]!.GetValue<string>(),
                body["errors"]![0]!["code"]!.GetValue<string>()));
        }

        Assert.Equal(["0"], await host.Database.QueryAsync("SELECT count(*)::text FROM project.portfolio_visual"));

        static void AssertMaps(JsonObject view)
        {
            JsonArray visuals = view["visuals"]!.AsArray();
            JsonNode city = visuals[0]!, approvedMap = visuals[3]!["children"]![0]!;
            Assert.Equal(("MAP", "CITY"), (city["visualType"]!.GetValue<string>(), city["groupBy"]!.GetValue<string>()));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse("{\"showList\":true,\"showCounts\":true}"), city["map"]));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse("{\"showList\":false,\"showCounts\":true}"), approvedMap["map"]));
            Assert.Null(visuals[1]!["map"]);
            Assert.Null(visuals[3]!["map"]);
            Assert.Equal("APPROVED_PLANNED", approvedMap["filters"]![0]!["values"]![0]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(visuals[1]!["groups"], city["groups"]));
            Assert.True(JsonNode.DeepEquals(visuals[2]!["groups"], approvedMap["groups"]));

            // The city filter keeps the test city's group alone; the approved map has it too, its count the projects counted (a count of projects, a whole number).
            JsonNode cityGroup = Assert.Single(city["groups"]!.AsArray())!;
            Assert.Equal(ProjectTestHost.CityId.ToString(), cityGroup["key"]!.GetValue<string>());
            Assert.True(cityGroup["projects"]!.GetValue<int>() >= 1);
            Assert.Equal(cityGroup["projects"]!.GetValue<int>(), cityGroup["values"]![0]!.GetValue<int>());
            JsonNode approvedInCity = approvedMap["groups"]!.AsArray().Single(g => g!["key"]?.GetValue<string>() == ProjectTestHost.CityId.ToString())!;
            Assert.True(approvedInCity["values"]![0]!.GetValue<int>() >= 1);
        }
    }

    [Fact]
    public async Task VisualsOfRisksCountTheRisksOfTheProjectsAndAreSavedAndReadBack()
    {
        using HttpClient client = host.Api.CreateClient();
        Sessions sessions = await client.SignInAsync();
        Guid projectId = await host.UnderReviewAsync(client, sessions);
        await host.DecideAndDeliverAsync(projectId, ApprovalTaskDecision.Approve);
        string manager = sessions.Approver;
        Guid open = await RiskAsync(projectId, "Supply delay", "IDENTIFIED");
        Guid closed = await RiskAsync(projectId, "Staff shortage", "CLOSED");

        using (HttpResponseMessage fieldsResponse = await client.GetAsync($"{Design}/fields?source=RISKS", manager))
        {
            Assert.Equal(HttpStatusCode.OK, fieldsResponse.StatusCode);
            JsonArray fields = (await fieldsResponse.ReadObjectAsync())["fields"]!.AsArray();
            Assert.Equal(("RISK.RISK", "COUNT"), (fields[0]!["key"]!.GetValue<string>(), fields[0]!["type"]!.GetValue<string>()));
            Assert.Equal(("TEXT", "ITEM"), (Field(fields, "RISK.TITLE")["type"]!.GetValue<string>(), Field(fields, "RISK.PROJECT")["type"]!.GetValue<string>()));
            Assert.Contains("CLOSED", Field(fields, "RISK.STATUS")["values"]!.AsArray().Select(v => v!.GetValue<string>()));
        }

        object[] count = [new { field = "RISK.RISK", aggregation = "COUNT" }];
        object design = DesignOf(
            Visual("Number of risks", "CARD", 0, 0, 3, 2, values: count, source: "RISKS"),
            Visual("Risks by status", "BAR", 3, 0, 6, 4, values: count, groupField: "RISK.STATUS", source: "RISKS"),
            Visual("Risk register", "TABLE", 0, 4, 12, 6, source: "RISKS", columns:
            [
                new { kind = "FIELD", key = "RISK.TITLE", aggregation = "NONE" },
                new { kind = "FIELD", key = "RISK.PROJECT", aggregation = "NONE" },
                new { kind = "FIELD", key = "RISK.STATUS", aggregation = "NONE" },
            ]));

        using HttpResponseMessage preview = await client.PostAsync($"{Design}/preview", manager, design);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        AssertRisks(await preview.ReadObjectAsync());

        using HttpResponseMessage saved = await client.PutAsync(Design, manager, design, ifMatch: null);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using (HttpResponseMessage read = await client.GetAsync(Design, manager))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            AssertRisks(await read.ReadObjectAsync());
        }

        Assert.Equal(["RISKS", "RISKS", "RISKS"], await host.Database.QueryAsync("SELECT data_fields->>'source' FROM project.portfolio_visual ORDER BY sort_order"));

        using HttpResponseMessage reset = await client.PostAsync($"{Design}/reset", manager);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        object gauge = DesignOf(Visual("Risk gauge", "GAUGE", 0, 0, 4, 3, values: count, source: "RISKS"));
        using HttpResponseMessage refused = await client.PutAsync(Design, manager, gauge, ifMatch: null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        JsonObject body = await refused.ReadObjectAsync();
        Assert.Equal(("PROJECT_PORTFOLIO_DESIGN_INVALID", "visuals[0].source", "NOT_ALLOWED"), (body["code"]!.GetValue<string>(),
            body["errors"]![0]!["field"]!.GetValue<string>(), body["errors"]![0]!["code"]!.GetValue<string>()));
        Assert.Equal(["0"], await host.Database.QueryAsync("SELECT count(*)::text FROM project.portfolio_visual"));

        void AssertRisks(JsonObject view)
        {
            JsonArray visuals = view["visuals"]!.AsArray();
            Assert.All(visuals, v => Assert.Equal("RISKS", v!["source"]!.GetValue<string>()));

            JsonNode card = Assert.Single(visuals[0]!["groups"]!.AsArray())!;
            Assert.Equal((2, 1), (card["values"]![0]!.GetValue<int>(), card["projects"]!.GetValue<int>()));
            Assert.Equal(
                [("CLOSED", 1), ("IDENTIFIED", 1)],
                visuals[1]!["groups"]!.AsArray().Select(g => (g!["key"]!.GetValue<string>(), g["values"]![0]!.GetValue<int>())).OrderBy(g => g.Item1));

            JsonNode table = visuals[2]!["table"]!;
            Assert.Equal(["TEXT", "ITEM", "CODE"], table["columnTypes"]!.AsArray().Select(t => t!.GetValue<string>()));
            JsonArray rows = table["rows"]!.AsArray();
            Assert.Equal(
                [(open.ToString(), "Supply delay", "IDENTIFIED"), (closed.ToString(), "Staff shortage", "CLOSED")],
                rows.Select(r => (r!["cells"]![0]!["key"]!.GetValue<string>(), r["cells"]![0]!["label"]!["en"]!.GetValue<string>(), r["cells"]![2]!["key"]!.GetValue<string>()))
                    .OrderByDescending(r => r.Item2, StringComparer.Ordinal));
            Assert.All(rows, r =>
            {
                Assert.Equal(projectId.ToString(), r!["cells"]![1]!["key"]!.GetValue<string>());
                Assert.Equal(projectId.ToString(), r["projectId"]!.GetValue<string>());
                Assert.False(string.IsNullOrEmpty(r["cells"]![1]!["label"]!["en"]!.GetValue<string>()));
            });
        }
    }

    /// <summary>A WF-06 risk of the project as the register holds it, written directly as the dashboard tests write theirs (its category need not be published).</summary>
    private async Task<Guid> RiskAsync(Guid projectId, string title, string status)
    {
        Guid id = Guid.NewGuid();
        (string closedAt, string closedBy, string rationale, string rationaleLang) = status == "CLOSED"
            ? ("now()", $"'{IdentityDatabase.SeedPrincipalId}'", "'Resolved.'", "'en'")
            : ("NULL", "NULL", "NULL", "NULL");
        await host.Database.ExecuteAsync($"""
            BEGIN; SET LOCAL session_replication_role = replica;
            INSERT INTO risk.risk (id, project_id, title, title_lang, description, description_lang, risk_category_item_id, status, identified_date, closed_at,
                                   closed_by_user_id, closure_rationale, closure_rationale_lang, reopened_count, created_at, created_by, updated_at, updated_by)
            VALUES ('{id}', '{projectId}', '{title}', 'en', 'A risk.', 'en', '{Guid.NewGuid()}', '{status}', current_date - 10, {closedAt},
                    {closedBy}, {rationale}, {rationaleLang}, 0, now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}');
            COMMIT;
            """);
        return id;
    }

    private static JsonNode Field(JsonArray fields, string key) => fields.Single(f => f!["key"]!.GetValue<string>() == key)!;

    private static int Int(JsonNode node, string property) => node[property]!.GetValue<int>();

    private static object DesignOf(params object[] visuals) => new { visuals };

    private static object Widget(string title, string widgetCode, decimal column, decimal row, decimal width, decimal height) =>
        Visual(title, "SECTION", column, row, width, height, section: "DASHBOARD_WIDGET", widgetCode: widgetCode);

    private static object Visual(
        string title, string type, decimal column, decimal row, decimal width, decimal height, string[]? measures = null, string groupBy = "NONE", object[]? values = null,
        object? axes = null, string? section = null, string? widgetCode = null, object[]? children = null, string[]? hiddenColumns = null, object[]? columns = null,
        object? text = null, object[]? filters = null, object? map = null, string? groupField = null, string? source = null) =>
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
            section,
            widgetCode,
            children,
            hiddenColumns,
            columns,
            text,
            filters,
            map,
            groupField,
            source,
        };
}
