namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>
/// The typed rows of one configuration version (ERD §5.3), as one document. A family carries only its own sections
/// (<see cref="ConfigurationSections"/>); every other section is empty. <see cref="Values"/> is open to every family.
/// </summary>
public sealed record ConfigurationContent
{
    public IReadOnlyList<ConfigurationValueEntry> Values { get; init; } = [];

    /// <summary>GOVERNANCE_PROFILE (ADR-015, TASK-105).</summary>
    public IReadOnlyList<GovernanceProfileEntry> GovernanceProfiles { get; init; } = [];

    /// <summary>MATERIALITY_BAND (ADR-016, TASK-106; values OQ-013).</summary>
    public IReadOnlyList<MaterialityBandEntry> MaterialityBands { get; init; } = [];

    /// <summary>RISK_MATRIX (ADR-011, PTBC-017).</summary>
    public IReadOnlyList<ProbabilityLevelEntry> ProbabilityLevels { get; init; } = [];

    /// <summary>RISK_MATRIX: five levels for each impact dimension (ADR-011; descriptions and boundaries OQ-006).</summary>
    public IReadOnlyList<ImpactLevelEntry> ImpactLevels { get; init; } = [];

    /// <summary>RISK_MATRIX: the ratings the matrix cells yield.</summary>
    public IReadOnlyList<RiskRatingEntry> RiskRatings { get; init; } = [];

    /// <summary>RISK_MATRIX: the 5×5 probability × overall-impact mapping (OQ-006).</summary>
    public IReadOnlyList<RiskMatrixCellEntry> RiskMatrixCells { get; init; } = [];

    /// <summary>APPROVAL_AUTHORITY (ADR-015/016; values OQ-005).</summary>
    public IReadOnlyList<ApprovalAuthorityEntry> ApprovalAuthority { get; init; } = [];

    /// <summary>NOTIFICATION_ROUTING: ADR-004's three matrices, one entry per event family.</summary>
    public IReadOnlyList<NotificationEventFamilyEntry> NotificationEventFamilies { get; init; } = [];

    /// <summary>KPI_POLICY (OQ-006).</summary>
    public IReadOnlyList<KpiPolicyEntry> KpiPolicies { get; init; } = [];

    /// <summary>PARTICIPATION (ADR-013).</summary>
    public IReadOnlyList<ParticipationRuleEntry> ParticipationRules { get; init; } = [];

    /// <summary>EVIDENCE_POLICY (PTBC-006, PTBC-019).</summary>
    public IReadOnlyList<EvidenceRequirementEntry> EvidenceRequirements { get; init; } = [];

    /// <summary>FIELD_CLASSIFICATION (ADR-010).</summary>
    public IReadOnlyList<FieldClassificationEntry> FieldClassifications { get; init; } = [];

    /// <summary>REPORT_RULES: the SCR-138 allowlist (ADR-019).</summary>
    public IReadOnlyList<ReportFieldEntry> ReportFields { get; init; } = [];
}
