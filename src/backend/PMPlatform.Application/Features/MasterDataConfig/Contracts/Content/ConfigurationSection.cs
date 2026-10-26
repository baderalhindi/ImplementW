namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>A section of <see cref="ConfigurationContent"/>; each maps to one or more typed tables of ERD §5.3.</summary>
public enum ConfigurationSection
{
    Values = 1,
    GovernanceProfiles = 2,
    MaterialityBands = 3,
    ProbabilityLevels = 4,
    ImpactLevels = 5,
    RiskRatings = 6,
    RiskMatrixCells = 7,
    ApprovalAuthority = 8,
    NotificationEventFamilies = 9,
    KpiPolicies = 10,
    ParticipationRules = 11,
    EvidenceRequirements = 12,
    FieldClassifications = 13,
    ReportFields = 14,
}
