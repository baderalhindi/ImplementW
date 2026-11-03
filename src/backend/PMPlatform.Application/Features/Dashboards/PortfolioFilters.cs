using System.Globalization;
using System.Text.Json;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>
/// The filters of a portfolio-wide dashboard (DSH-CC-17, widened by the 27 Oct 2026 review to entity, region and city, and by the 2 Nov 2026
/// review to sector, status, priority, strategic portfolio, initiative and period): each offers only the values of the projects the caller may see
/// there, with how many projects have each, so a filter narrows the population and can never widen it.
/// </summary>
internal sealed record PortfolioFilters(IReadOnlyList<PortfolioFilters.Dimension> Dimensions)
{
    public static readonly PortfolioFilters None = new([]);

    /// <summary>
    /// Every filter, by its query name: the values a project has for it — one, or none, except the period, every year its planned period
    /// touches — and the value the context chose.
    /// </summary>
    private static readonly (string Field, Func<ProjectFacts, IEnumerable<string>> Of, Func<DashboardContext, string?> Chosen)[] Fields =
    [
        ("classificationItemId", p => One(Id(p.ClassificationItemId)), c => Id(c.ClassificationItemId)),
        ("externalEntityId", p => One(Id(p.ExternalEntityId)), c => Id(c.ExternalEntityId)),
        ("departmentId", p => One(Id(p.DepartmentId)), c => Id(c.DepartmentId)),
        ("regionItemId", p => One(Id(p.RegionItemId)), c => Id(c.RegionItemId)),
        ("cityItemId", p => One(Id(p.CityItemId)), c => Id(c.CityItemId)),
        ("status", p => [StatusCode(p.Status)], c => c.Status is { } s ? StatusCode(s) : null),
        ("priorityItemId", p => One(Id(p.PriorityItemId)), c => Id(c.PriorityItemId)),
        ("strategicPortfolioItemId", p => One(Id(p.StrategicPortfolioItemId)), c => Id(c.StrategicPortfolioItemId)),
        ("strategicInitiativeItemId", p => One(Id(p.StrategicInitiativeItemId)), c => Id(c.StrategicInitiativeItemId)),
        ("period", p => ProjectPeriods.YearsOf(p.PlannedStartDate, p.PlannedEndDate).Select(y => y.ToString(CultureInfo.InvariantCulture)),
            c => c.PeriodYear?.ToString(CultureInfo.InvariantCulture)),
    ];

    public IReadOnlyList<Guid> Departments => Ids("departmentId");

    public IReadOnlyList<Guid> Entities => Ids("externalEntityId");

    public IReadOnlyList<Guid> Regions => Ids("regionItemId");

    public IReadOnlyList<Guid> Cities => Ids("cityItemId");

    /// <summary>The master data items some filter offers — sector, place, priority, portfolio, initiative — which the view names.</summary>
    public IReadOnlyList<Guid> Items =>
        [.. Dimensions.Where(d => d.Field.EndsWith("ItemId", StringComparison.Ordinal)).SelectMany(d => d.Options).Select(o => Guid.Parse(o.Value))];

    public static PortfolioFilters Of(IReadOnlyList<ProjectFacts> projects) => new(
        [.. Fields.Select(f => new Dimension(f.Field, [.. projects
            .SelectMany(f.Of)
            .GroupBy(v => v, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new DashboardFilterOption(g.Key, g.Count()))]))]);

    /// <summary>The first filter whose value is not among the options, by its query name; null when every one is.</summary>
    public string? Refused(DashboardContext context) =>
        Fields.FirstOrDefault(f => f.Chosen(context) is { } chosen
                                   && !Dimensions.Single(d => d.Field == f.Field).Options.Any(o => o.Value == chosen)).Field;

    public static bool Matches(ProjectFacts project, DashboardContext context) =>
        Fields.All(f => f.Chosen(context) is not { } chosen || f.Of(project).Contains(chosen, StringComparer.Ordinal));

    /// <summary>The query name of the first filter the context sets.</summary>
    public static string FirstField(DashboardContext context) => Fields.FirstOrDefault(f => f.Chosen(context) is not null).Field ?? "departmentId";

    /// <summary>The projects counted by region and city, largest first.</summary>
    public static IReadOnlyList<DashboardLocationCount> Locations(IReadOnlyList<ProjectFacts> projects) =>
        [.. projects
            .GroupBy(p => (p.RegionItemId, p.CityItemId))
            .Select(g => new DashboardLocationCount(g.Key.RegionItemId, g.Key.CityItemId, g.Count()))
            .OrderByDescending(l => l.ProjectCount).ThenBy(l => l.RegionItemId).ThenBy(l => l.CityItemId)];

    public IReadOnlyList<DashboardFilter> ToView() => [.. Dimensions.Select(d => new DashboardFilter(d.Field, d.Options))];

    private List<Guid> Ids(string field) =>
        [.. Dimensions.FirstOrDefault(d => d.Field == field)?.Options.Select(o => Guid.Parse(o.Value)) ?? []];

    private static string? Id(Guid? id) => id?.ToString("D");

    private static IEnumerable<string> One(string? value) => value is null ? [] : [value];

    private static string StatusCode(Domain.Project.ProjectLifecycleState state) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(state.ToString());

    /// <summary>One filter and the values it offers, each with its count of projects.</summary>
    public sealed record Dimension(string Field, IReadOnlyList<DashboardFilterOption> Options);
}
