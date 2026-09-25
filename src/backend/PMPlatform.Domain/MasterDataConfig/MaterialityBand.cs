using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>
/// One of the three change-routing bands of a governance profile (TASK-106, ADR-016). Values outstanding (OQ-013).
/// Delete policy: CASCADE.
/// </summary>
public sealed class MaterialityBand : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    public Guid GovernanceProfileItemId { get; set; }

    /// <summary>1, 2 or 3.</summary>
    public short BandNo { get; set; }

    /// <summary>Percent of the active baseline budget.</summary>
    public decimal? CostThresholdPct { get; set; }

    public Money? CostThresholdSar { get; set; }

    /// <summary>Percent of the baseline duration.</summary>
    public decimal? ScheduleThresholdPct { get; set; }

    public int? ScheduleThresholdDays { get; set; }

    /// <summary>Scope escalates by rule.</summary>
    public string? ScopeRuleCode { get; set; }

    /// <summary>Band 1 may be recorded without approval.</summary>
    public bool RequiresApproval { get; set; }
}
