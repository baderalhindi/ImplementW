using PMPlatform.Application.Features.Dashboards;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Dashboards.Projections;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Tests.Unit.Application.Dashboards;

/// <summary>
/// PROJECT.PROJECT_PROFILE (the 18 Nov 2026 designer fields): the project record's own attributes as registered fields, a value not set being missing (never 0
/// or false), and the registration budget withheld from a caller without <c>FINANCIAL_VIEW_SENSITIVE</c>.
/// </summary>
public sealed class ProjectProfileSourceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    private static readonly ProjectionContract Profile = DashboardProjections.Find(DashboardProjections.ProjectProfile)!;

    private static ProjectFacts Facts(Money? budget = null, bool? strategic = null, DateOnly? plannedEnd = null) => new(
        Guid.NewGuid(), "P-1", Guid.NewGuid(), null, null, ProjectLifecycleState.Active, Guid.NewGuid(), null,
        new DateTimeOffset(2026, 3, 1, 21, 30, 0, TimeSpan.FromHours(3)), ParticipationMode.AhdaManaged, new NarrativeText("مشروع", Language.Ar),
        PlannedEndDate: plannedEnd, CurrentStagePercent: 40.5m, ProgressMethod: ProjectProgressMethod.Stages, RegistrationBudgetSar: budget,
        DeclaredActualPercent: 12.25m, FundingSource: FundingSource.StrategyBudgetPartial, IsStrategic: strategic);

    private static (string? Value, bool IsMasked) Read(Observation observation, string field) => Profile.Field(field)!.Read(observation.Data!);

    [Fact]
    public void ItIsRegisteredAboutProjectsOnProjectView()
    {
        Assert.Equal(("WF-01", ProjectionGrain.Project, "PROJECT_VIEW", false), (Profile.SourceDomain, Profile.Grain, Profile.PermissionCode, Profile.IsSensitive));
        Assert.Equal(ProjectionEligibility.AnyProject, Profile.Eligibility);
        Assert.Contains(DashboardProjections.Reportable, p => p.Code == DashboardProjections.ProjectProfile);
        Assert.Equal(16, Profile.Fields.Count);
        Assert.Equal(ProjectProfileSource.Fields.Select(f => f.Code), Profile.Fields.Select(f => f.Code));
    }

    /// <summary>Every field reads a figure the source states, so none throws or silently reads nothing.</summary>
    [Fact]
    public void EveryFieldIsAFigureTheSourceStates()
    {
        Observation observation = ProjectProfileSource.Observe(Facts(new Money(1500000m), true, new DateOnly(2027, 6, 30)), amounts: true, Now);

        Assert.Equal(Profile.Fields.Select(f => f.Code).Order(StringComparer.Ordinal), observation.Data!.Figures.Select(f => f.Measure).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheBudgetIsMaskedWithoutTheSensitiveFinancialScope()
    {
        ProjectFacts facts = Facts(new Money(1500000m));

        Assert.Equal(("1500000.00", false), Read(ProjectProfileSource.Observe(facts, amounts: true, Now), ProjectProfileSource.RegistrationBudget));
        Observation withheld = ProjectProfileSource.Observe(facts, amounts: false, Now);
        Assert.Equal((null, true), Read(withheld, ProjectProfileSource.RegistrationBudget));
        Assert.Equal([ProjectProfileSource.RegistrationBudget], withheld.MaskedFields);
        Assert.Equal(("12.25", false), Read(withheld, "DECLARED_ACTUAL_PERCENT"));
    }

    [Fact]
    public void ValuesAreTheSourcesExactWords()
    {
        Observation observation = ProjectProfileSource.Observe(Facts(strategic: false, plannedEnd: new DateOnly(2027, 6, 30)), amounts: true, Now);

        Assert.Equal("false", Read(observation, "IS_STRATEGIC").Value);
        Assert.Equal("2027-06-30", Read(observation, "PLANNED_END_DATE").Value);
        Assert.Equal("2026-03-01T18:30:00Z", Read(observation, "ACTIVATED_AT").Value);
        Assert.Equal("STRATEGY_BUDGET_PARTIAL", Read(observation, "FUNDING_SOURCE").Value);
        Assert.Equal("AHDA_MANAGED", Read(observation, "PARTICIPATION_MODE").Value);
        Assert.Equal("STAGES", Read(observation, "PROGRESS_METHOD").Value);
        Assert.Equal("40.5", Read(observation, "CURRENT_STAGE_PERCENT").Value);
    }

    /// <summary>A value not set is missing: no 0 for a budget, no false for a flag, no date.</summary>
    [Fact]
    public void AValueNotSetIsMissing()
    {
        Observation observation = ProjectProfileSource.Observe(Facts(), amounts: true, Now);

        Assert.All(
            ["REGISTRATION_BUDGET", "IS_STRATEGIC", "ENTITY_UPDATE_COMPLIANT", "PLANNED_END_DATE", "CLOSED_AT", "NEXT_ACTION_DUE_DATE", "DECLARED_PLANNED_PERCENT"],
            code => Assert.Equal((null, false), Read(observation, code)));
        Assert.Null(observation.MaskedFields);
    }
}
