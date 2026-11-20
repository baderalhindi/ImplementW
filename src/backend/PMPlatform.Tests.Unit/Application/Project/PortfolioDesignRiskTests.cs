using System.Text.Json.Nodes;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Dashboards;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.Insights;
using PMPlatform.Application.Features.Risk;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;
using PMPlatform.Domain.Risk;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Tests.Unit.Application.Project;

/// <summary>
/// The 20 Nov 2026 record sources (dashboard-designer-record-sources.md): the risk fields, the rules of a visual of risks, its figures — counts by rating,
/// status and category, the project dimensions' groups and filters, the risk fields' filters, the risk table one row per risk — and the reader's RISK_VIEW
/// scope.
/// </summary>
public sealed class PortfolioDesignRiskTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);
    private static readonly OnPlanThresholds Thresholds = new(5m, 10m);
    private static readonly Guid Health = Guid.NewGuid();
    private static readonly Guid Education = Guid.NewGuid();
    private static readonly Guid Operational = Guid.NewGuid();
    private static readonly Guid Financial = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    /// <summary>The data catalogue a visual of projects names.</summary>
    private static readonly IReadOnlyList<PortfolioFieldDescriptor> ProjectFields =
        [.. PortfolioFieldReader.Catalogue(DashboardProjections.Reportable.Select(p => p.ToDescriptor()))];

    /// <summary>The risk fields, as the reader offers them over a matrix of three ratings, three probabilities and three impacts.</summary>
    private static readonly IReadOnlyList<PortfolioFieldDescriptor> RiskFields = Reader(new Configuration(Matrix())).FieldsAsync(Now, CancellationToken.None).Result;

    private static readonly Dictionary<string, PortfolioFieldDescriptor> RiskCatalogue = RiskFields.ToDictionary(f => f.Key, StringComparer.Ordinal);

    private static FieldIssue? Issue(params PortfolioVisualInput[] visuals) => PortfolioDesignService.Issue(new PortfolioDesignInput(visuals), ProjectFields, RiskFields);

    private static PortfolioFieldValue Count(string field = PortfolioRiskFields.Risk) => new(field, PortfolioFieldAggregation.Count);

    /// <summary>A visual of risks: the number of risks, grouped by <paramref name="groupField"/> (or ungrouped for a card).</summary>
    private static PortfolioVisualInput Risks(
        PortfolioVisualType type = PortfolioVisualType.Bar, string? groupField = PortfolioRiskFields.Rating, PortfolioGroupBy groupBy = PortfolioGroupBy.None,
        IReadOnlyList<PortfolioFieldFilter>? filters = null, IReadOnlyList<PortfolioFieldValue>? values = null, PortfolioDateUnit? unit = null) =>
        new(new BilingualLabel("عدد المخاطر", "Number of risks"), type, [], groupBy, 0, 0, 6, 4, null, values ?? [Count()], groupField, filters, unit,
            Source: PortfolioRecordSource.Risks);

    private static PortfolioVisualInput RiskCard() => Risks(PortfolioVisualType.Card, groupField: null);

    private static PortfolioVisualInput RiskTable(params PortfolioTableColumn[] columns) =>
        new(new BilingualLabel("سجل المخاطر", "Risk register"), PortfolioVisualType.Table, [], PortfolioGroupBy.None, 0, 0, 12, 6, Columns: columns,
            Source: PortfolioRecordSource.Risks);

    private static PortfolioTableColumn Field(string key, PortfolioTableAggregation aggregation = PortfolioTableAggregation.None) =>
        new(PortfolioTableColumnKind.Field, key, aggregation);

    // The fields.

    [Fact]
    public void TheRiskFieldsAreListedWithTheirTypes()
    {
        Assert.Equal(
            [
                ("RISK.RISK", PortfolioFieldType.Count), ("RISK.TITLE", PortfolioFieldType.Text), ("RISK.PROJECT", PortfolioFieldType.Item),
                ("RISK.STATUS", PortfolioFieldType.Code), ("RISK.CATEGORY", PortfolioFieldType.Item), ("RISK.OWNER", PortfolioFieldType.Item),
                ("RISK.RATING", PortfolioFieldType.Code), ("RISK.PROBABILITY", PortfolioFieldType.Code), ("RISK.IMPACT", PortfolioFieldType.Code),
                ("RISK.SCORE", PortfolioFieldType.Count), ("RISK.IDENTIFIED_DATE", PortfolioFieldType.Date), ("RISK.NEXT_REVIEW_DATE", PortfolioFieldType.Date),
                ("RISK.CLOSED_DATE", PortfolioFieldType.Date), ("RISK.REVIEW_OVERDUE", PortfolioFieldType.Boolean), ("RISK.MATERIALISED", PortfolioFieldType.Boolean),
                ("RISK.OPEN_ACTIONS", PortfolioFieldType.Count), ("RISK.REOPENED", PortfolioFieldType.Count),
            ],
            RiskFields.Select(f => (f.Key, f.Type)));
        Assert.All(RiskFields, f => Assert.Equal(("RISK", "WF-06", PortfolioFieldGrain.Project, false), (f.ProjectionCode, f.SourceDomain, f.Grain, f.IsSensitive)));
        Assert.Equal("TITLE", RiskCatalogue[PortfolioRiskFields.Title].FieldCode);
    }

    [Fact]
    public void ACodeFieldListsItsCodesAndTheMatrixGivesTheRatingsAndLevels()
    {
        Assert.Equal(["IDENTIFIED", "ASSESSED", "TREATMENT", "MONITORING", "CLOSED"], RiskCatalogue[PortfolioRiskFields.Status].Values);
        Assert.Equal(["LOW", "MEDIUM", "HIGH"], RiskCatalogue[PortfolioRiskFields.Rating].Values);
        Assert.Equal(["1", "2", "3"], RiskCatalogue[PortfolioRiskFields.Probability].Values);
        Assert.Equal(["1", "2", "3"], RiskCatalogue[PortfolioRiskFields.Impact].Values);
    }

    [Fact]
    public void TheNumberOfRisksIsOnlyCountedAndTextAndItemsAreCounted()
    {
        Assert.Equal([PortfolioFieldAggregation.Count], RiskCatalogue[PortfolioRiskFields.Risk].Aggregations);
        Assert.Equal([PortfolioFieldAggregation.Count], RiskCatalogue[PortfolioRiskFields.Title].Aggregations);
        Assert.Equal([PortfolioFieldAggregation.Count], RiskCatalogue[PortfolioRiskFields.Owner].Aggregations);
        Assert.Equal(PortfolioFieldDescriptor.AggregationsOf(PortfolioFieldType.Count), RiskCatalogue[PortfolioRiskFields.Score].Aggregations);
    }

    [Fact]
    public async Task WithNoMatrixInForceTheRatingsAndLevelsHaveNoCodes()
    {
        IReadOnlyList<PortfolioFieldDescriptor> fields = await Reader(new Configuration(null)).FieldsAsync(Now, CancellationToken.None);

        Assert.Empty(fields.Single(f => f.Key == PortfolioRiskFields.Rating).Values);
        Assert.Equal(17, fields.Count);
    }

    // The rules.

    [Theory]
    [InlineData(PortfolioVisualType.Bar)]
    [InlineData(PortfolioVisualType.Column)]
    [InlineData(PortfolioVisualType.Pie)]
    [InlineData(PortfolioVisualType.Donut)]
    public void AChartCountsRisks(PortfolioVisualType type) => Assert.Null(Issue(Risks(type)));

    [Fact]
    public void ACardALineAndATableCountRisks()
    {
        Assert.Null(Issue(RiskCard()));
        Assert.Null(Issue(Risks(PortfolioVisualType.Line, PortfolioRiskFields.IdentifiedDate, unit: PortfolioDateUnit.Month)));
        Assert.Null(Issue(RiskTable(Field(PortfolioRiskFields.Title), Field(PortfolioRiskFields.Project), Field(PortfolioRiskFields.Rating))));
        Assert.Null(Issue(Risks(PortfolioVisualType.Table, groupField: null)));
    }

    [Theory]
    [InlineData(PortfolioVisualType.Gauge)]
    [InlineData(PortfolioVisualType.Map)]
    public void AGaugeAndAMapDoNotCountRisks(PortfolioVisualType type) =>
        Assert.Equal(new FieldIssue("visuals[0].source", FieldIssue.NotAllowed), Issue(Risks(type, groupField: null, groupBy: PortfolioGroupBy.City)));

    [Fact]
    public void ASectionATextBoxAndAContainerDoNotCountRisks()
    {
        PortfolioVisualInput section = new(new BilingualLabel("حسب القطاع", "By sector"), PortfolioVisualType.Section, [], PortfolioGroupBy.None, 0, 0, 12, 6,
            PortfolioSection.SectorTable, Source: PortfolioRecordSource.Risks);
        PortfolioVisualInput text = new(new BilingualLabel("ملاحظة", "Note"), PortfolioVisualType.Text, [], PortfolioGroupBy.None, 0, 0, 4, 2,
            Text: new PortfolioTextBox("ملاحظة", "Note", PortfolioTextSize.Normal, PortfolioTextAlign.Start, Bold: false), Source: PortfolioRecordSource.Risks);
        PortfolioVisualInput container = new(new BilingualLabel("مجموعة", "Group"), PortfolioVisualType.Container, [], PortfolioGroupBy.None, 0, 0, 6, 4,
            Children: [], Source: PortfolioRecordSource.Risks);

        Assert.Equal(new FieldIssue("visuals[0].source", FieldIssue.NotAllowed), Issue(section));
        Assert.Equal(new FieldIssue("visuals[0].source", FieldIssue.NotAllowed), Issue(text));
        Assert.Equal(new FieldIssue("visuals[0].source", FieldIssue.NotAllowed), Issue(container));
    }

    [Fact]
    public void ASourceOutsideTheListIsRefusedAndProjectsIsTheDefault()
    {
        Assert.Equal(new FieldIssue("visuals[0].source", FieldIssue.NotAllowed), Issue(RiskCard() with { Source = (PortfolioRecordSource)9 }));
        PortfolioVisualInput projects = new(new BilingualLabel("المشاريع", "Projects"), PortfolioVisualType.Card, [PortfolioMeasure.Projects], PortfolioGroupBy.None, 0, 0, 4, 2,
            Source: PortfolioRecordSource.Projects);
        Assert.Null(Issue(projects));
    }

    [Fact]
    public void AVisualOfRisksDrawsNoPortfolioFigure()
    {
        Assert.Equal(new FieldIssue("visuals[0].measures", FieldIssue.NotAllowed), Issue(RiskCard() with { Measures = [PortfolioMeasure.Projects] }));
        Assert.Equal(new FieldIssue("visuals[0].values", FieldIssue.Required), Issue(RiskCard() with { Values = null }));
        Assert.Equal(
            new FieldIssue("visuals[0].columns[1].kind", FieldIssue.NotAllowed),
            Issue(RiskTable(Field(PortfolioRiskFields.Title), new PortfolioTableColumn(PortfolioTableColumnKind.Measure, "PROJECTS", null))));
    }

    [Fact]
    public void AVisualOfRisksNamesRiskFieldsOnlyAndAVisualOfProjectsNone()
    {
        const string spend = "FINANCIAL_KPI.FINANCIAL_POSITION.ACTUAL_EXPENDITURE_TO_DATE";
        Assert.Equal(new FieldIssue("visuals[0].values", FieldIssue.NotAllowed), Issue(Risks(values: [new(spend, PortfolioFieldAggregation.Sum)])));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.NotAllowed), Issue(Risks(groupField: "PROGRESS.PROJECT_HEALTH_STATUS.OVERALL_HEALTH")));
        Assert.Equal(new FieldIssue("visuals[0].values", FieldIssue.NotAllowed), Issue(Risks() with { Source = null }));
        Assert.Equal(new FieldIssue("visuals[0].columns[0].key", FieldIssue.NotAllowed), Issue(RiskTable(Field(spend))));
    }

    [Fact]
    public void AVisualOfRisksGroupsByAProjectDimensionOrARiskField()
    {
        Assert.Null(Issue(Risks(groupField: null, groupBy: PortfolioGroupBy.Sector)));
        Assert.Null(Issue(Risks(groupField: PortfolioRiskFields.Category)));
        Assert.Null(Issue(Risks(groupField: PortfolioRiskFields.ReviewOverdue)));
        Assert.Null(Issue(Risks(groupField: PortfolioRiskFields.NextReviewDate, unit: PortfolioDateUnit.Year)));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.NotAllowed), Issue(Risks(groupField: PortfolioRiskFields.Title)));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.NotAllowed), Issue(Risks(groupField: PortfolioRiskFields.Score)));
        Assert.Equal(new FieldIssue("visuals[0].groupField", FieldIssue.NotAllowed), Issue(Risks(groupField: PortfolioRiskFields.Status, groupBy: PortfolioGroupBy.Sector)));
    }

    [Fact]
    public void AVisualOfRisksFiltersOnRiskFieldsAndProjectDimensions()
    {
        IReadOnlyList<PortfolioFieldFilter> filters =
        [
            new(PortfolioRiskFields.Status, ["IDENTIFIED", "CLOSED"]), new(PortfolioRiskFields.Owner, [Owner.ToString()]), new(PortfolioRiskFields.ReviewOverdue, ["TRUE"]),
            new(PortfolioRiskFields.IdentifiedDate, ["2026"]), new("PROJECT.SECTOR", [Health.ToString()]),
        ];
        Assert.Null(Issue(Risks(filters: filters)));
        Assert.Equal(new FieldIssue("visuals[0].filters", FieldIssue.NotAllowed), Issue(Risks(filters: [new(PortfolioRiskFields.Title, ["X"])])));
        Assert.Equal(new FieldIssue("visuals[0].filters", FieldIssue.NotAllowed), Issue(Risks(filters: [new(PortfolioRiskFields.Category, ["OPERATIONAL"])])));
    }

    [Fact]
    public void ARiskTableColumnTakesTheCalculationsItsFieldAllows()
    {
        Assert.Null(Issue(RiskTable(Field(PortfolioRiskFields.Project), Field(PortfolioRiskFields.Title, PortfolioTableAggregation.CountDistinct),
            Field(PortfolioRiskFields.Score, PortfolioTableAggregation.Average), new PortfolioTableColumn(PortfolioTableColumnKind.Dimension, "SECTOR", PortfolioTableAggregation.None))));
        Assert.Equal(new FieldIssue("visuals[0].columns[0].aggregation", FieldIssue.NotAllowed), Issue(RiskTable(Field(PortfolioRiskFields.Title, PortfolioTableAggregation.Sum))));
    }

    // What is stored and echoed.

    [Fact]
    public void ASourceOfRisksIsStoredAndEchoedAndProjectsIsNot()
    {
        string stored = PortfolioDesignService.DataFieldsOf(Risks())!;
        Assert.Equal("RISKS", JsonNode.Parse(stored)!["source"]!.GetValue<string>());
        Assert.False(JsonNode.Parse(PortfolioDesignService.DataFieldsOf(Risks() with { Source = PortfolioRecordSource.Projects })!)!.AsObject().ContainsKey("source"));

        var types = RiskFields.ToDictionary(f => f.Key, f => f.Type, StringComparer.Ordinal);
        PortfolioVisualResult result = PortfolioDesignService.ResultOf(Risks(), _ => [], types);
        Assert.Equal(PortfolioRecordSource.Risks, result.Source);
        Assert.Equal([PortfolioFieldType.Count], result.ValueTypes);
        Assert.Null(PortfolioDesignService.ResultOf(Risks() with { Source = PortfolioRecordSource.Projects }, _ => [], types).Source);
        Assert.Equal(PortfolioRecordSource.Risks, PortfolioDesignService.ResultOf(RiskTable(Field(PortfolioRiskFields.Title)), _ => [], types, _ => null).Source);

        PortfolioVisual saved = new()
        {
            Id = Guid.NewGuid(),
            SortOrder = 1,
            TitleAr = "عدد المخاطر",
            TitleEn = "Number of risks",
            VisualType = PortfolioVisualType.Bar,
            Measures = [],
            GroupBy = PortfolioGroupBy.None,
            LayoutColumn = 0,
            LayoutRow = 0,
            LayoutWidth = 6,
            LayoutHeight = 4,
            DataFields = stored,
        };
        Assert.Equal(PortfolioRecordSource.Risks, Assert.Single(PortfolioDesignService.Nested([saved])).Source);
    }

    // The figures.

    private static PortfolioProjectRow Project(Guid sector, string title = "مشروع") =>
        new(new PMPlatform.Domain.Project.Project
        {
            Id = Guid.NewGuid(),
            Title = new NarrativeText(title, Language.Ar),
            LifecycleState = ProjectLifecycleState.Active,
            ClassificationItemId = sector,
        }, ProjectIndicators.None);

    private static PortfolioRecordRow Risk(
        PortfolioProjectRow project, string status = "IDENTIFIED", string? rating = null, Guid? category = null, Guid? owner = null, bool overdue = false,
        string identified = "2026-03-01")
    {
        var id = Guid.NewGuid();
        return new PortfolioRecordRow(id, project.Project.Id, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [PortfolioRiskFields.Risk] = "1",
            [PortfolioRiskFields.Title] = id.ToString(),
            [PortfolioRiskFields.Project] = project.Project.Id.ToString(),
            [PortfolioRiskFields.Status] = status,
            [PortfolioRiskFields.Rating] = rating,
            [PortfolioRiskFields.Category] = (category ?? Operational).ToString(),
            [PortfolioRiskFields.Owner] = owner?.ToString(),
            [PortfolioRiskFields.ReviewOverdue] = overdue ? "true" : "false",
            [PortfolioRiskFields.IdentifiedDate] = identified,
        });
    }

    private static PortfolioRecords Records(IReadOnlyDictionary<string, BilingualLabel>? labels, params PortfolioRecordRow[] rows) =>
        new(rows, labels ?? new Dictionary<string, BilingualLabel>(StringComparer.Ordinal));

    private static IReadOnlyList<PortfolioVisualGroup> Groups(PortfolioVisualInput visual, IReadOnlyList<PortfolioProjectRow> rows, PortfolioRecords records) =>
        PortfolioDesignService.Groups(visual, rows, null, amounts: true, Thresholds, new Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>>(),
            RiskCatalogue, records);

    private static PortfolioTableResult Table(PortfolioVisualInput visual, IReadOnlyList<PortfolioProjectRow> rows, PortfolioRecords records) =>
        PortfolioDesignService.Table(visual, rows, null, amounts: true, Thresholds, new Dictionary<Guid, IReadOnlyDictionary<string, IReadOnlyList<PortfolioFieldCell>>>(),
            RiskCatalogue, records.Labels, records);

    [Fact]
    public void ACardCountsTheRisksOfItsProjectsClosedOnesIncluded()
    {
        PortfolioProjectRow a = Project(Health), b = Project(Education), none = Project(Health);
        PortfolioRecords records = Records(null, Risk(a), Risk(a, "CLOSED"), Risk(b));

        PortfolioVisualGroup card = Assert.Single(Groups(RiskCard(), [a, b, none], records));

        Assert.Equal(3m, card.Values[0]);
        Assert.Equal((2, 3), (card.Projects, card.Measured));
    }

    [Fact]
    public void ARiskOfAProjectOutsideThePopulationIsNotCounted()
    {
        PortfolioProjectRow seen = Project(Health), unseen = Project(Health);
        PortfolioRecords records = Records(null, Risk(seen), Risk(unseen), Risk(unseen));

        Assert.Equal(1m, Assert.Single(Groups(RiskCard(), [seen], records)).Values[0]);
    }

    [Fact]
    public void RisksAreCountedByRatingTheUnassessedLast()
    {
        PortfolioProjectRow a = Project(Health);
        PortfolioRecords records = Records(null, Risk(a, rating: "HIGH"), Risk(a, rating: "HIGH"), Risk(a, rating: "LOW"), Risk(a));

        Assert.Equal([("HIGH", 2m), ("LOW", 1m), (null, 1m)], Groups(Risks(), [a], records).Select(g => (g.Key, g.Values[0])));
    }

    [Fact]
    public void RisksAreCountedByStatusClosedOnesUnderClosed()
    {
        PortfolioProjectRow a = Project(Health);
        PortfolioRecords records = Records(null, Risk(a, "CLOSED"), Risk(a, "CLOSED"), Risk(a, "MONITORING"));

        Assert.Equal([("CLOSED", 2m), ("MONITORING", 1m)], Groups(Risks(groupField: PortfolioRiskFields.Status), [a], records).Select(g => (g.Key, g.Values[0])));
    }

    [Fact]
    public void RisksAreCountedByCategoryEachGroupNamed()
    {
        PortfolioProjectRow a = Project(Health);
        Dictionary<string, BilingualLabel> labels = new(StringComparer.Ordinal)
        {
            [Operational.ToString()] = new("تشغيلية", "Operational"),
            [Financial.ToString()] = new("مالية", "Financial"),
        };
        PortfolioRecords records = Records(labels, Risk(a), Risk(a), Risk(a, category: Financial));

        IReadOnlyList<PortfolioVisualGroup> groups = Groups(Risks(groupField: PortfolioRiskFields.Category), [a], records);

        Assert.Equal([(Operational.ToString(), "Operational", 2m), (Financial.ToString(), "Financial", 1m)], groups.Select(g => (g.Key, g.Label?.En, g.Values[0])));
    }

    [Fact]
    public void RisksAreGroupedByTheirProjectsDimension()
    {
        PortfolioProjectRow a = Project(Health), b = Project(Health), c = Project(Education);
        PortfolioRecords records = Records(null, Risk(a), Risk(b), Risk(b), Risk(c));

        IReadOnlyList<PortfolioVisualGroup> groups = Groups(Risks(groupField: null, groupBy: PortfolioGroupBy.Sector), [a, b, c], records);

        Assert.Equal([(Health.ToString(), 3m, 2), (Education.ToString(), 1m, 1)], groups.Select(g => (g.Key, g.Values[0], g.Projects)));
    }

    [Fact]
    public void AProjectDimensionFilterKeepsTheRisksOfItsProjects()
    {
        PortfolioProjectRow a = Project(Health), c = Project(Education);
        PortfolioRecords records = Records(null, Risk(a), Risk(a), Risk(c));

        Assert.Equal(2m, Assert.Single(Groups(RiskCard() with { Filters = [new("PROJECT.SECTOR", [Health.ToString()])] }, [a, c], records)).Values[0]);
    }

    [Fact]
    public void ARiskFieldFilterKeepsTheRisksWithItsValue()
    {
        PortfolioProjectRow a = Project(Health);
        PortfolioRecords records = Records(
            null, Risk(a, owner: Owner, identified: "2025-12-31"), Risk(a, owner: Owner), Risk(a, "CLOSED", owner: Owner), Risk(a), Risk(a, overdue: true));

        decimal? Counted(params PortfolioFieldFilter[] filters) => Assert.Single(Groups(RiskCard() with { Filters = filters }, [a], records)).Values[0];

        Assert.Equal(4m, Counted(new PortfolioFieldFilter(PortfolioRiskFields.Status, ["identified"])));
        Assert.Equal(3m, Counted(new PortfolioFieldFilter(PortfolioRiskFields.Owner, [Owner.ToString().ToUpperInvariant()])));
        Assert.Equal(2m, Counted(new PortfolioFieldFilter(PortfolioRiskFields.Owner, [Owner.ToString()]), new PortfolioFieldFilter(PortfolioRiskFields.IdentifiedDate, ["2026"])));
        Assert.Equal(1m, Counted(new PortfolioFieldFilter(PortfolioRiskFields.ReviewOverdue, ["TRUE"])));
    }

    [Fact]
    public void ARiskTableListsEachRiskOnItsRowWithItsProjectAndOwner()
    {
        PortfolioProjectRow a = Project(Health, "مشروع أ"), b = Project(Education, "مشروع ب");
        PortfolioRecordRow first = Risk(a, rating: "HIGH", owner: Owner), second = Risk(a, "CLOSED"), third = Risk(b, rating: "LOW", owner: Owner);
        Dictionary<string, BilingualLabel> labels = new(StringComparer.Ordinal)
        {
            [first.Id.ToString()] = new("تأخر التوريد", "تأخر التوريد"),
            [second.Id.ToString()] = new("نقص الكوادر", "نقص الكوادر"),
            [third.Id.ToString()] = new("تأخر التوريد", "تأخر التوريد"),
            [a.Project.Id.ToString()] = new("مشروع أ", "مشروع أ"),
            [b.Project.Id.ToString()] = new("مشروع ب", "مشروع ب"),
            [Owner.ToString()] = new("سارة", "سارة"),
        };

        PortfolioTableResult table = Table(
            RiskTable(Field(PortfolioRiskFields.Title), Field(PortfolioRiskFields.Project), Field(PortfolioRiskFields.Owner), Field(PortfolioRiskFields.Rating),
                Field(PortfolioRiskFields.Risk, PortfolioTableAggregation.Count)),
            [a, b], Records(labels, first, second, third));

        Assert.Equal(
            [PortfolioTableColumnType.Text, PortfolioTableColumnType.Item, PortfolioTableColumnType.Item, PortfolioTableColumnType.Code, PortfolioTableColumnType.Count],
            table.ColumnTypes);

        // Two risks of the same title are two rows: a risk's title cell is keyed by the risk.
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal([first.Id.ToString(), third.Id.ToString(), second.Id.ToString()], table.Rows.Select(r => r.Cells[0].Key));
        PortfolioTableRow row = table.Rows[0];
        Assert.Equal(("تأخر التوريد", a.Project.Id.ToString(), "مشروع أ", "سارة", "HIGH", 1m),
            (row.Cells[0].Label?.Ar, row.Cells[1].Key, row.Cells[1].Label?.Ar, row.Cells[2].Label?.Ar, row.Cells[3].Key, row.Cells[4].Value));
        Assert.Equal((1, a.Project.Id), (row.Projects, row.ProjectId));
        Assert.Null(table.Rows[2].Cells[2].Key);
        Assert.Equal(3m, table.Totals![4].Value);
        Assert.False(table.Truncated);
    }

    [Fact]
    public void ARiskTableGroupedByRatingCountsItsRisksAndTheirProjects()
    {
        PortfolioProjectRow a = Project(Health), b = Project(Education);
        PortfolioTableResult table = Table(
            RiskTable(Field(PortfolioRiskFields.Rating), Field(PortfolioRiskFields.Risk, PortfolioTableAggregation.Count),
                Field(PortfolioRiskFields.Project, PortfolioTableAggregation.CountDistinct)),
            [a, b], Records(null, Risk(a, rating: "HIGH"), Risk(a, rating: "HIGH"), Risk(b, rating: "HIGH"), Risk(b, rating: "LOW")));

        Assert.Equal([("HIGH", 3m, 2m, 2), ("LOW", 1m, 1m, 1)], table.Rows.Select(r => (r.Cells[0].Key, r.Cells[1].Value, r.Cells[2].Value, r.Projects)));
        Assert.All(table.Rows, r => Assert.Null(r.ProjectId));
    }

    // A risk's values.

    [Fact]
    public void ARisksValuesAreReadFromItsRegisterEntry()
    {
        Guid project = Guid.NewGuid(), category = Guid.NewGuid();
        RiskDetail risk = Detail(project, category, RiskStatus.Monitoring, nextReview: Today.AddDays(-1), assessment: (2, 3, "HIGH"), openActions: 2) with
        {
            MaterialisedAt = Now,
            ReopenedCount = 1,
            ClosedAt = null,
        };

        Dictionary<string, string?> values = PortfolioRiskReader.ValuesOf(risk, Today);

        Assert.Equal("1", values[PortfolioRiskFields.Risk]);
        Assert.Equal(risk.Id.ToString(), values[PortfolioRiskFields.Title]);
        Assert.Equal(project.ToString(), values[PortfolioRiskFields.Project]);
        Assert.Equal(category.ToString(), values[PortfolioRiskFields.Category]);
        Assert.Equal(Owner.ToString(), values[PortfolioRiskFields.Owner]);
        Assert.Equal(("MONITORING", "HIGH", "2", "3", "6"), (values[PortfolioRiskFields.Status], values[PortfolioRiskFields.Rating], values[PortfolioRiskFields.Probability],
            values[PortfolioRiskFields.Impact], values[PortfolioRiskFields.Score]));
        Assert.Equal(("2026-03-01", "2026-11-19", null), (values[PortfolioRiskFields.IdentifiedDate], values[PortfolioRiskFields.NextReviewDate], values[PortfolioRiskFields.ClosedDate]));
        Assert.Equal(("true", "true", "2", "1"), (values[PortfolioRiskFields.ReviewOverdue], values[PortfolioRiskFields.Materialised], values[PortfolioRiskFields.OpenActions],
            values[PortfolioRiskFields.Reopened]));
    }

    [Fact]
    public void AnUnassessedRiskHasNoRatingAndAClosedRiskIsNeverOverdue()
    {
        Dictionary<string, string?> unassessed = PortfolioRiskReader.ValuesOf(Detail(Guid.NewGuid(), Guid.NewGuid(), RiskStatus.Identified, nextReview: null), Today);
        Assert.Equal((null, null, null, null), (unassessed[PortfolioRiskFields.Rating], unassessed[PortfolioRiskFields.Probability], unassessed[PortfolioRiskFields.Impact],
            unassessed[PortfolioRiskFields.Score]));
        Assert.Equal(("false", "false"), (unassessed[PortfolioRiskFields.ReviewOverdue], unassessed[PortfolioRiskFields.Materialised]));

        RiskDetail closed = Detail(Guid.NewGuid(), Guid.NewGuid(), RiskStatus.Closed, nextReview: Today.AddDays(-30)) with { ClosedAt = new DateTimeOffset(2026, 10, 5, 23, 30, 0, TimeSpan.FromHours(-3)) };
        Dictionary<string, string?> values = PortfolioRiskReader.ValuesOf(closed, Today);
        Assert.Equal(("CLOSED", "false", "2026-10-06"), (values[PortfolioRiskFields.Status], values[PortfolioRiskFields.ReviewOverdue], values[PortfolioRiskFields.ClosedDate]));

        Dictionary<string, string?> dueToday = PortfolioRiskReader.ValuesOf(Detail(Guid.NewGuid(), Guid.NewGuid(), RiskStatus.Assessed, nextReview: Today), Today);
        Assert.Equal("false", dueToday[PortfolioRiskFields.ReviewOverdue]);
    }

    private static RiskDetail Detail(
        Guid project, Guid category, RiskStatus status, DateOnly? nextReview, (short Probability, short Impact, string Rating)? assessment = null, int openActions = 0) =>
        new(
            Guid.NewGuid(), project, new NarrativeText("تأخر التوريد", Language.Ar), new NarrativeText("وصف", Language.Ar), category, Owner, status, new DateOnly(2026, 3, 1),
            nextReview,
            assessment is { } a
                ? new RiskAssessmentSummary(Guid.NewGuid(), 1, Now, Guid.NewGuid(), a.Probability, a.Impact, new RiskRatingDetail(Guid.NewGuid(), a.Rating, new BilingualLabel("عالية", "High")))
                : null,
            null, null, [], null, null, null, 0, Now, Guid.NewGuid(), Now, Guid.NewGuid(),
            [.. Enumerable.Range(0, openActions).Select(_ => new RiskOpenTreatmentAction(Guid.NewGuid(), new NarrativeText("إجراء", Language.Ar), RiskTreatmentActionType.Mitigate,
                RiskTreatmentActionStatus.Planned, null))]);

    // The reader's scope.

    [Fact]
    public async Task TheReaderReadsOnlyTheRisksOfProjectsTheCallersRiskViewScopeReaches()
    {
        var department = Guid.NewGuid();
        ProjectFacts reached = Facts(department), other = Facts(Guid.NewGuid()), notAsked = Facts(department);
        RiskEntity open = Entity(reached.Id), closed = Entity(reached.Id, RiskStatus.Closed), hidden = Entity(other.Id), elsewhere = Entity(notAsked.Id);
        Repository repository = new([open, closed, hidden, elsewhere]);
        Authorization engine = new(new RecordScope([new RecordScopeClause { DepartmentId = department }]));

        PortfolioRecords records = await Reader(new Configuration(Matrix()), repository, engine, [reached, other, notAsked])
            .ReadAsync(Owner, [reached.Id, other.Id], Now, CancellationToken.None);

        Assert.Equal(new[] { open.Id, closed.Id }.Order(), records.Rows.Select(r => r.Id).Order());
        Assert.Equal([reached.Id], repository.AskedProjects);
        Assert.Equal(PermissionCatalogue.RiskView, engine.Asked);
        Assert.Equal("مشروع", records.Labels[reached.Id.ToString()].Ar);
        Assert.Equal("تأخر التوريد", records.Labels[open.Id.ToString()].Ar);
        Assert.Equal("Operational", records.Labels[Operational.ToString()].En);
        Assert.Equal("سارة", records.Labels[Owner.ToString()].Ar);
    }

    [Fact]
    public async Task ACallerWithoutRiskViewReadsNoRisk()
    {
        ProjectFacts project = Facts(Guid.NewGuid());
        Repository repository = new([Entity(project.Id)]);

        PortfolioRecords records = await Reader(new Configuration(Matrix()), repository, new Authorization(RecordScope.None), [project])
            .ReadAsync(Owner, [project.Id], Now, CancellationToken.None);

        Assert.Empty(records.Rows);
        Assert.Null(repository.AskedProjects);
    }

    private static ProjectFacts Facts(Guid department) =>
        new(Guid.NewGuid(), null, department, null, null, ProjectLifecycleState.Active, Guid.NewGuid(), null, null, ParticipationMode.AhdaManaged,
            new NarrativeText("مشروع", Language.Ar));

    private static RiskEntity Entity(Guid project, RiskStatus status = RiskStatus.Identified) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = project,
            Title = new NarrativeText("تأخر التوريد", Language.Ar),
            Description = new NarrativeText("وصف", Language.Ar),
            RiskCategoryItemId = Operational,
            OwnerUserId = Owner,
            Status = status,
            IdentifiedDate = new DateOnly(2026, 3, 1),
        };

    private static ConfigurationContent Matrix() =>
        new()
        {
            RiskRatings = [new RiskRatingEntry("HIGH", new BilingualLabel("عالية", "High"), 3), new RiskRatingEntry("LOW", new BilingualLabel("منخفضة", "Low"), 1),
                new RiskRatingEntry("MEDIUM", new BilingualLabel("متوسطة", "Medium"), 2)],
            ProbabilityLevels = [.. Enumerable.Range(1, 3).Select(l => new ProbabilityLevelEntry((short)l, new BilingualLabel("مستوى", "Level"), null, null))],
            ImpactLevels = [.. new[] { Guid.NewGuid(), Guid.NewGuid() }.SelectMany(d => Enumerable.Range(1, 3)
                .Select(l => new ImpactLevelEntry(d, (short)l, new BilingualLabel("مستوى", "Level"), null, null, null)))],
        };

    private static PortfolioRiskReader Reader(
        Configuration configuration, Repository? repository = null, Authorization? engine = null, IReadOnlyList<ProjectFacts>? projects = null)
    {
        Repository risks = repository ?? new Repository([]);
        return new PortfolioRiskReader(
            risks, new Projects(projects ?? []), engine ?? new Authorization(RecordScope.None), new RiskViews(risks, configuration, new Issues()), configuration,
            new MasterData(), new Names());
    }

    private sealed class Configuration(ConfigurationContent? matrix) : IConfigurationResolver
    {
        public Task<ResolvedConfiguration> ResolveAsync(string familyCode, DateTimeOffset asOf, CancellationToken cancellationToken) =>
            matrix is null
                ? throw new ConfigurationMissingException(familyCode, ConfigurationMissingReason.NoEffectiveVersion, "none")
                : Task.FromResult(new ResolvedConfiguration(Guid.NewGuid(), familyCode, 1, asOf.AddDays(-1), null, matrix));

        public Task<ResolvedConfiguration> ResolvePinnedAsync(Guid versionId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<RiskRatingReference>> ListRiskRatingsAsync(Guid versionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Projects(IReadOnlyList<ProjectFacts> all) : IProjectFactsReader
    {
        public Task<ProjectFacts?> FindAsync(Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ProjectFacts?> FindByFormalProjectIdAsync(string formalProjectId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ProjectFacts>> ListReachedAsync(RecordScope scope, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectFacts>>([.. all.Where(p => scope.Matches(new AuthorizationSubject
            {
                ProjectId = p.Id, DepartmentId = p.DepartmentId, ExternalEntityId = p.ExternalEntityId, OwnerUserId = p.ProjectManagerUserId,
            }))]);
    }

    private sealed class Authorization(RecordScope scope) : IAuthorizationEngine
    {
        public string? Asked { get; private set; }

        public Task<RecordScope> GetRecordScopeAsync(Guid userId, string permissionCode, CancellationToken cancellationToken)
        {
            Asked = permissionCode;
            return Task.FromResult(scope);
        }

        public Task<AuthorizationPrincipal?> GetPrincipalAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AuthorizationDecision> AuthorizeAsync(Guid userId, AuthorizationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AuthorizationDecision> EvaluateAsync(Guid userId, AuthorizationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<FieldMask> GetFieldMaskAsync(Guid userId, string permissionCode, string entityCode, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class MasterData : IMasterDataResolver
    {
        public Task<MasterDataItemReference> RequirePublishedItemAsync(string catalogueCode, Guid itemId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<MasterDataItemReference>> ListPublishedItemsAsync(string catalogueCode, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MasterDataItemReference>>([new(Operational, catalogueCode, "OPERATIONAL", new BilingualLabel("تشغيلية", "Operational"), null, 1)]);
    }

    private sealed class Names : IDisplayNameService
    {
        public Task<DisplayNames> ListAsync(Guid callerId, DisplayNameQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new DisplayNames([.. query.UserIds.Select(id => new PersonName(id, "سارة"))]));

        public Task<OrganizationUnits> ListUnitsAsync(Guid callerId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Issues : IRiskIssueMaterialisation
    {
        public Task<AdministrationResult<OriginatedIssue>> RaiseIssueAsync(RiskIssueCommand command, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<OriginatedIssue>> ListByOriginatingRisksAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OriginatedIssue>>([]);
    }

    /// <summary>The risk register's rows, with no assessment, acceptance or action; it records which projects it was asked for.</summary>
    private sealed class Repository(IReadOnlyList<RiskEntity> risks) : IRiskRepository
    {
        public IReadOnlyList<Guid>? AskedProjects { get; private set; }

        public Task<IReadOnlyList<RiskEntity>> ListRisksAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
        {
            AskedProjects = [.. projectIds];
            return Task.FromResult<IReadOnlyList<RiskEntity>>([.. risks.Where(r => projectIds.Contains(r.ProjectId))]);
        }

        public Task<IReadOnlyList<RiskAssessmentVersion>> ListLatestAssessmentsAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RiskAssessmentVersion>>([]);

        public Task<IReadOnlyList<RiskAcceptance>> ListActiveAcceptancesAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RiskAcceptance>>([]);

        public Task<IReadOnlyList<RiskTreatmentAction>> ListOpenActionsAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RiskTreatmentAction>>([]);

        public Task<IRiskWork> BeginAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RiskEntity?> FindRiskAsync(Guid riskId, uint? expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(IReadOnlyList<RiskEntity> Items, int TotalCount)> PageRisksAsync(RiskQuery query, PageRequest page, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RiskStatus?> FindStatusAsync(Guid riskId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(IReadOnlyList<RiskAssessmentVersion> Items, int TotalCount)> PageAssessmentsAsync(Guid riskId, PageRequest page, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RiskAssessmentVersion?> FindAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<RiskAssessmentImpact>> ListImpactsAsync(IReadOnlyCollection<Guid> assessmentIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RiskAcceptance?> FindActiveAcceptanceAsync(Guid riskId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(IReadOnlyList<RiskAcceptance> Items, int TotalCount)> PageAcceptancesAsync(Guid riskId, PageRequest page, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RiskAcceptance>> ListLapsedAcceptancesAsync(DateOnly today, RiskAcceptance? after, int batchSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasOpenActionAsync(Guid riskId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(IReadOnlyList<RiskTreatmentAction> Items, int TotalCount)> PageActionsAsync(Guid riskId, PageRequest page, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RiskTreatmentAction?> FindActionAsync(Guid actionId, uint? expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> CountOpenUnacceptedAsync(Guid projectId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<RiskEntity>> ListOpenRisksAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) => throw new NotSupportedException();

        public uint RowVersionOf(RiskEntity risk) => throw new NotSupportedException();

        public uint RowVersionOf(RiskTreatmentAction action) => throw new NotSupportedException();

        public void Add(RiskEntity risk) => throw new NotSupportedException();

        public void Add(RiskAssessmentVersion assessment) => throw new NotSupportedException();

        public void Add(RiskAssessmentImpact impact) => throw new NotSupportedException();

        public void Add(RiskTreatmentAction action) => throw new NotSupportedException();

        public void Add(RiskAcceptance acceptance) => throw new NotSupportedException();

        public Task<RiskSaveOutcome> SaveAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
