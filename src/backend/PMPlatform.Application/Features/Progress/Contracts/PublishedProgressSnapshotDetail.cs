using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>A PUBLISHED/OFFICIAL period record, as it was published; nothing changes it afterwards.</summary>
public sealed record PublishedProgressSnapshotDetail(
    Guid Id,
    Guid ProjectId,
    Guid ReportingCycleId,
    Guid ProgressSubmissionId,
    DateTimeOffset PublishedAt,
    Guid PublishedByUserId,
    decimal ActualPercent,
    bool IsOverridden,
    decimal? PlannedPercent,
    HealthStatus OverallHealth,
    HealthStatus? ScheduleHealth,
    HealthStatus? FinancialStatus,
    Guid HealthRuleConfigurationVersionId);
