using System.Net;
using System.Text.Json.Nodes;
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
/// BI-style table) previews, saves and reads back with its table of figures; a card with columns is refused.
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

    private static int Int(JsonNode node, string property) => node[property]!.GetValue<int>();

    private static object DesignOf(params object[] visuals) => new { visuals };

    private static object Widget(string title, string widgetCode, decimal column, decimal row, decimal width, decimal height) =>
        Visual(title, "SECTION", column, row, width, height, section: "DASHBOARD_WIDGET", widgetCode: widgetCode);

    private static object Visual(
        string title, string type, decimal column, decimal row, decimal width, decimal height, string[]? measures = null, string groupBy = "NONE", object[]? values = null,
        object? axes = null, string? section = null, string? widgetCode = null, object[]? children = null, string[]? hiddenColumns = null, object[]? columns = null) =>
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
        };
}
