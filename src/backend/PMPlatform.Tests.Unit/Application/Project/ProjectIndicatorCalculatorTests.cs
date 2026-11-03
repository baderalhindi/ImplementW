using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.FollowUp;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Tests.Unit.Application.Project;

/// <summary>
/// RPMO's on-plan indicator and follow-up reasons (the 2 Nov 2026 review §2): the gap in points between actual and planned progress; on track
/// above −5, late from −5 to just above −10, stalled at −10 or worse; not calculated for a project not started or closed, nor when a
/// percentage is missing; and the follow-up order stalled, suspended, late, overdue action, open obstacle.
/// </summary>
public sealed class ProjectIndicatorCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 3);

    [Theory]
    [InlineData(50, 50, OnPlanStatus.OnTrack)]
    [InlineData(50, 45.01, OnPlanStatus.OnTrack)]
    [InlineData(50, 45, OnPlanStatus.Late)]
    [InlineData(50, 40.01, OnPlanStatus.Late)]
    [InlineData(50, 40, OnPlanStatus.Stalled)]
    [InlineData(86, 20, OnPlanStatus.Stalled)]
    public void TheGapSetsTheIndicatorAtTheDefaultThresholds(double planned, double actual, OnPlanStatus expected)
    {
        ProjectIndicators indicators = ProjectIndicatorCalculator.Of(Project(ProjectLifecycleState.Active, (decimal)planned, (decimal)actual), OnPlanThresholds.Default, Today);

        Assert.Equal(expected, indicators.OnPlan);
        Assert.Equal((decimal)actual - (decimal)planned, indicators.GapPoints);
    }

    [Theory]
    [InlineData(ProjectLifecycleState.ApprovedPlanned)]
    [InlineData(ProjectLifecycleState.Closed)]
    [InlineData(ProjectLifecycleState.Draft)]
    public void NoGapForAProjectNotStartedOrClosed(ProjectLifecycleState state) =>
        Assert.Null(ProjectIndicatorCalculator.Of(Project(state, 50, 10), OnPlanThresholds.Default, Today).GapPoints);

    [Fact]
    public void NoGapWhenAPercentageIsMissing() =>
        Assert.Null(ProjectIndicatorCalculator.Of(Project(ProjectLifecycleState.Active, null, 10), OnPlanThresholds.Default, Today).OnPlan);

    [Fact]
    public void TheReasonsComeInRpmosFollowUpOrder()
    {
        ProjectEntity project = Project(ProjectLifecycleState.Suspended, 80, 20);
        project.NextAction = new NarrativeText("متابعة", Language.Ar);
        project.NextActionDueDate = Today.AddDays(-1);
        project.CurrentObstacle = new NarrativeText("تعارض", Language.Ar);

        ProjectIndicators indicators = ProjectIndicatorCalculator.Of(project, OnPlanThresholds.Default, Today);

        Assert.Equal([FollowUpReason.Stalled, FollowUpReason.Suspended, FollowUpReason.ActionOverdue, FollowUpReason.OpenObstacle], indicators.FollowUpReasons);
        Assert.Equal(ActionDueStatus.Overdue, indicators.ActionDue);
    }

    [Fact]
    public void ConfiguredThresholdsReplaceTheDefaults() =>
        Assert.Equal(OnPlanStatus.OnTrack, ProjectIndicatorCalculator.Of(Project(ProjectLifecycleState.Active, 50, 44), new OnPlanThresholds(8, 15), Today).OnPlan);

    [Fact]
    public void DaysRemainingCountToThePlannedEnd()
    {
        ProjectEntity project = Project(ProjectLifecycleState.Active, null, null);
        project.PlannedEndDate = Today.AddDays(30);

        Assert.Equal(30, ProjectIndicatorCalculator.Of(project, OnPlanThresholds.Default, Today).DaysRemaining);
    }

    private static ProjectEntity Project(ProjectLifecycleState state, decimal? planned, decimal? actual) => new()
    {
        Title = new NarrativeText("مشروع", Language.Ar),
        LifecycleState = state,
        DeclaredPlannedPercent = planned,
        DeclaredActualPercent = actual,
    };
}

/// <summary>Entity update compliance (the 2 Nov 2026 review): calculated from on-time answers, overridden by what RPMO records.</summary>
public sealed class EntityUpdateComplianceTests
{
    private static readonly DateOnly Today = new(2026, 11, 3);

    [Theory]
    [InlineData(5, 4, true)]
    [InlineData(5, 3, false)]
    [InlineData(10, 8, true)]
    public void AnEntityIsCompliantWhenEnoughAnswersCameOnTime(int counted, int onTime, bool compliant)
    {
        EntityUpdateCompliance? result = ProjectIndicatorCalculator.Of(Project(null), OnPlanThresholds.Default, Today, new UpdateComplianceCounts(counted, onTime)).EntityUpdates;

        Assert.Equal(compliant, result?.Calculated);
        Assert.Equal(compliant, result?.Effective);
        Assert.False(result?.Overridden);
    }

    [Fact]
    public void WhatRpmoRecordedOverridesTheCalculation()
    {
        EntityUpdateCompliance? result = ProjectIndicatorCalculator.Of(Project(false), OnPlanThresholds.Default, Today, new UpdateComplianceCounts(4, 4)).EntityUpdates;

        Assert.True(result?.Calculated);
        Assert.False(result?.Effective);
        Assert.True(result?.Overridden);
    }

    [Fact]
    public void WithNoRequestAndNoRecordComplianceIsNotKnown() =>
        Assert.Null(ProjectIndicatorCalculator.Of(Project(null), OnPlanThresholds.Default, Today).EntityUpdates);

    private static PMPlatform.Domain.Project.Project Project(bool? recorded) => new()
    {
        Title = new NarrativeText("مشروع", Language.Ar),
        LifecycleState = ProjectLifecycleState.Active,
        EntityUpdateCompliant = recorded,
    };
}
