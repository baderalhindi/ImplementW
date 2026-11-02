using PMPlatform.Application.Features.Dashboards;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Tests.Unit.Application.Dashboards;

/// <summary>The portfolio filters (DSH-CC-17, widened by the 27 Oct 2026 review): each offers only its population's values, narrows and never widens.</summary>
public sealed class PortfolioFiltersTests
{
    private static readonly Guid Infra = Guid.NewGuid();
    private static readonly Guid Urban = Guid.NewGuid();
    private static readonly Guid Entity = Guid.NewGuid();
    private static readonly Guid North = Guid.NewGuid();
    private static readonly Guid South = Guid.NewGuid();
    private static readonly Guid NorthCity = Guid.NewGuid();

    private static readonly ProjectFacts[] Projects =
    [
        Project(Infra, null, North, NorthCity),
        Project(Infra, Entity, South, null),
        Project(Urban, null, North, NorthCity),
        Project(Urban, null, null, null),
    ];

    [Fact]
    public void TheOptionsAreThePopulationsOwnValues()
    {
        PortfolioFilters filters = PortfolioFilters.Of(Projects);
        Assert.Equal(new[] { Infra, Urban }.Order(), filters.Departments);
        Assert.Equal([Entity], filters.Entities);
        Assert.Equal(new[] { North, South }.Order(), filters.Regions);
        Assert.Equal([NorthCity], filters.Cities);
    }

    [Fact]
    public void AValueOutsideTheOptionsIsRefusedByItsQueryName()
    {
        PortfolioFilters filters = PortfolioFilters.Of(Projects);
        Assert.Null(filters.Refused(new DashboardContext(null, Infra, Entity, South, NorthCity)));
        Assert.Equal("externalEntityId", filters.Refused(new DashboardContext(null, Infra, Guid.NewGuid())));
        Assert.Equal("cityItemId", filters.Refused(new DashboardContext(null, null, CityItemId: Guid.NewGuid())));
    }

    [Fact]
    public void EveryFilterSetNarrowsThePopulation()
    {
        Assert.Equal(2, Projects.Count(p => PortfolioFilters.Matches(p, new DashboardContext(null, null, RegionItemId: North))));
        Assert.Single(Projects, p => PortfolioFilters.Matches(p, new DashboardContext(null, Urban, RegionItemId: North)));
        Assert.Equal(Projects.Length, Projects.Count(p => PortfolioFilters.Matches(p, new DashboardContext(null, null))));
    }

    [Fact]
    public void LocationsCountTheProjectsByRegionAndCityLargestFirst()
    {
        IReadOnlyList<DashboardLocationCount> locations = PortfolioFilters.Locations(Projects);
        Assert.Equal(new DashboardLocationCount(North, NorthCity, 2), locations[0]);
        Assert.Equal(4, locations.Sum(l => l.ProjectCount));
        Assert.Contains(new DashboardLocationCount(null, null, 1), locations);
    }

    private static ProjectFacts Project(Guid department, Guid? entity, Guid? region, Guid? city) =>
        new(Guid.NewGuid(), "PRJ-000001", department, entity, null, ProjectLifecycleState.Active, Guid.NewGuid(), null, null, ParticipationMode.AhdaManaged,
            new NarrativeText("Project", Language.En), region, city);
}
