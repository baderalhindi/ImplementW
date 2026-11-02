using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>
/// The filters of a portfolio-wide dashboard (DSH-CC-17, widened by the 27 Oct 2026 review to entity, region and city): each offers only
/// the values of the projects the caller may see there, so a filter narrows the population and can never widen it.
/// </summary>
internal sealed record PortfolioFilters(IReadOnlyList<Guid> Departments, IReadOnlyList<Guid> Entities, IReadOnlyList<Guid> Regions, IReadOnlyList<Guid> Cities)
{
    public static readonly PortfolioFilters None = new([], [], [], []);

    public static PortfolioFilters Of(IReadOnlyList<ProjectFacts> projects) => new(
        Distinct(projects.Select(p => (Guid?)p.DepartmentId)),
        Distinct(projects.Select(p => p.ExternalEntityId)),
        Distinct(projects.Select(p => p.RegionItemId)),
        Distinct(projects.Select(p => p.CityItemId)));

    /// <summary>The first filter whose value is not among the options, by its query name; null when every one is.</summary>
    public string? Refused(DashboardContext context) =>
        Outside(context.DepartmentId, Departments) ? "departmentId"
        : Outside(context.ExternalEntityId, Entities) ? "externalEntityId"
        : Outside(context.RegionItemId, Regions) ? "regionItemId"
        : Outside(context.CityItemId, Cities) ? "cityItemId"
        : null;

    public static bool Matches(ProjectFacts project, DashboardContext context) =>
        (context.DepartmentId is not { } department || project.DepartmentId == department)
        && (context.ExternalEntityId is not { } entity || project.ExternalEntityId == entity)
        && (context.RegionItemId is not { } region || project.RegionItemId == region)
        && (context.CityItemId is not { } city || project.CityItemId == city);

    /// <summary>The query name of the first filter the context sets.</summary>
    public static string FirstField(DashboardContext context) =>
        context.DepartmentId is not null ? "departmentId"
        : context.ExternalEntityId is not null ? "externalEntityId"
        : context.RegionItemId is not null ? "regionItemId"
        : "cityItemId";

    /// <summary>The projects counted by region and city, largest first.</summary>
    public static IReadOnlyList<DashboardLocationCount> Locations(IReadOnlyList<ProjectFacts> projects) =>
        [.. projects
            .GroupBy(p => (p.RegionItemId, p.CityItemId))
            .Select(g => new DashboardLocationCount(g.Key.RegionItemId, g.Key.CityItemId, g.Count()))
            .OrderByDescending(l => l.ProjectCount).ThenBy(l => l.RegionItemId).ThenBy(l => l.CityItemId)];

    private static bool Outside(Guid? value, IReadOnlyList<Guid> options) => value is { } chosen && !options.Contains(chosen);

    private static List<Guid> Distinct(IEnumerable<Guid?> values) => [.. values.OfType<Guid>().Distinct().Order()];
}
