namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>One of the three change-routing bands of a governance profile (ADR-016).</summary>
public sealed record MaterialityBandEntry(
    Guid GovernanceProfileItemId,
    short BandNo,
    decimal? CostThresholdPct,
    decimal? CostThresholdSar,
    decimal? ScheduleThresholdPct,
    int? ScheduleThresholdDays,
    string? ScopeRuleCode,
    bool RequiresApproval);
