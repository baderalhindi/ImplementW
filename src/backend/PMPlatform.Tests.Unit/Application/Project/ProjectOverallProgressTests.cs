using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.FollowUp;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Tests.Unit.Application.Project;

/// <summary>
/// Review round 8, point 02: the stage method's overall figure is Σ weight × completion over the stages that apply, and the gap and the on-plan
/// label are taken from the project's one overall figure rather than from the values declared on it.
/// </summary>
public sealed class ProjectOverallProgressTests
{
    private static readonly DateOnly Today = new(2026, 11, 15);

    [Fact]
    public void TheClientsWorkedExampleIs55Percent()
    {
        // Weights 10, 15, 10, 50, 10 and 5; the first three complete and implementation 40% done: 10 + 15 + 10 + 50 × 40% = 55.
        decimal? overall = ProjectOverallProgressReader.StageOverall(
        [
            (10m, true, 100m), (15m, true, 100m), (10m, true, 100m), (50m, true, 40m), (10m, true, 0m), (5m, true, 0m),
        ]);

        Assert.Equal(55m, overall);
    }

    [Fact]
    public void AStageThatDoesNotApplyTakesItsWeightOut()
    {
        // Without the 15-weight stage the same completions weigh 10 + 10 + 20 over 85.
        decimal? overall = ProjectOverallProgressReader.StageOverall(
        [
            (10m, true, 100m), (15m, false, null), (10m, true, 100m), (50m, true, 40m), (10m, true, 0m), (5m, true, 0m),
        ]);

        Assert.Equal(47.06m, overall);
    }

    [Fact]
    public void AStageWithNoCompletionLeavesTheFigureUnmeasured() =>
        Assert.Null(ProjectOverallProgressReader.StageOverall([(50m, true, 100m), (50m, true, null)]));

    [Fact]
    public void TheGapComesFromTheOverallFigureNotTheDeclaredValues()
    {
        ProjectEntity project = new()
        {
            Id = Guid.NewGuid(),
            Title = new NarrativeText("مشروع", Language.Ar),
            LifecycleState = ProjectLifecycleState.Active,
            ProgressMethod = ProjectProgressMethod.Stages,
            DeclaredPlannedPercent = 50m,
            DeclaredActualPercent = 50m,
        };
        ProjectOverallProgress progress = new(ProjectProgressMethod.Stages, ProgressFigureSource.Stages, 30m, 50m, Today, "EXECUTION", [], false, false);

        ProjectIndicators indicators = ProjectIndicatorCalculator.Of(project, OnPlanThresholds.Default, Today, null, progress);

        Assert.Equal(-20m, indicators.GapPoints);
        Assert.Equal(OnPlanStatus.Stalled, indicators.OnPlan);
        Assert.Same(progress, indicators.Progress);
    }
}
