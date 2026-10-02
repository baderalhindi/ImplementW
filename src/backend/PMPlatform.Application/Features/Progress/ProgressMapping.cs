using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

internal static class ProgressMapping
{
    public static ProgressSubmissionDetail ToDetail(ProgressSubmission s) => new(
        s.Id, s.ProjectId, s.ReportingCycleId, s.RevisionNo, s.Status, s.EffectiveActualPercent, s.ActualPercentCalculated, s.ActualPercentOverride,
        s.OverrideReason, s.IsOverridden, s.PlannedPercent, s.BaselineId, s.Narrative, s.ProjectIntakeId, s.SubmittedByUserId, s.SubmittedAt,
        s.ReviewedByUserId, s.ReviewedAt, s.ReturnReason, s.CreatedAt, s.CreatedBy, s.UpdatedAt, s.UpdatedBy);

    public static ReportingCycleSummary ToSummary(ReportingCycle c) => new(c.Id, c.ProjectId, c.PeriodStart, c.PeriodEnd, c.DueDate, c.Status);

    public static PublishedProgressSnapshotDetail ToDetail(PublishedProgressSnapshot s) => new(
        s.Id, s.ProjectId, s.ReportingCycleId, s.ProgressSubmissionId, s.PublishedAt, s.PublishedByUserId, s.ActualPercent, s.IsOverridden,
        s.PlannedPercent, s.OverallHealth, s.ScheduleHealth, s.FinancialStatus, s.HealthRuleConfigurationVersionId);

    public static ProjectHealthStatusDetail ToDetail(ProjectHealthStatus h) =>
        new(h.Id, h.ProjectId, h.OverallHealth, h.ActualPercent, h.PlannedPercent, h.ComputedAt, h.HealthRuleConfigurationVersionId);
}
