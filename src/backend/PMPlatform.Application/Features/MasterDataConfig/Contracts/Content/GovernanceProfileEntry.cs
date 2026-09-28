namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>The switches of one governance profile (ADR-015). The assignment thresholds are OQ-014's and may be absent.</summary>
public sealed record GovernanceProfileEntry(
    Guid GovernanceProfileItemId,
    bool RequiresBaselineApproval,
    bool RiskManagementRequired,
    short ChangeBandCount,
    int UpdateCadenceDays,
    Guid DocumentControlLevelItemId,
    bool IncludedInReporting,
    decimal? AssignmentMinBudgetSar,
    int? AssignmentMinDurationDays,
    IReadOnlyList<string> MandatoryFieldCodes);
