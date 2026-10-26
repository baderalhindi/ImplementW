using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>
/// The switches for one governance profile (Light, Standard, Full — GOVERNANCE_PROFILE master data items) inside a
/// configuration version (TASK-105, ADR-015). Delete policy: CASCADE.
/// </summary>
public sealed class GovernanceProfileSetting : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    public Guid GovernanceProfileItemId { get; set; }

    /// <summary>Light skips baseline approval; Full cannot.</summary>
    public bool RequiresBaselineApproval { get; set; }

    /// <summary>Light carries issues only (ADR-015).</summary>
    public bool RiskManagementRequired { get; set; }

    public short ChangeBandCount { get; set; }

    public int UpdateCadenceDays { get; set; }

    public Guid DocumentControlLevelItemId { get; set; }

    public bool IncludedInReporting { get; set; }

    /// <summary>Assignment-by-rule threshold; value outstanding (OQ-014).</summary>
    public Money? AssignmentMinBudgetSar { get; set; }

    /// <summary>Assignment-by-rule threshold; value outstanding (OQ-014).</summary>
    public int? AssignmentMinDurationDays { get; set; }
}
