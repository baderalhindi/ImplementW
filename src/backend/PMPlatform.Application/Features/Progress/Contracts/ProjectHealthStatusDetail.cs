using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>The CURRENT/LIVE Overall Project Health, computed from the derived figures, with when it was computed.</summary>
public sealed record ProjectHealthStatusDetail(
    Guid Id, Guid ProjectId, HealthStatus OverallHealth, decimal? ActualPercent, decimal? PlannedPercent, DateTimeOffset ComputedAt, Guid HealthRuleConfigurationVersionId);
