using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// A commitment's amount in one Etimad cost category (ADR-008 extension: the breakdown aligns to Etimad from day one). Launch is
/// project total only, so no operation writes one yet (financial-kpi.md D-4). Delete policy: CASCADE.
/// </summary>
public sealed class FinancialCommitmentLine : AuditedEntity
{
    public Guid FinancialCommitmentId { get; set; }

    /// <summary>A master data item of catalogue ETIMAD_COST_CATEGORY.</summary>
    public Guid EtimadCategoryItemId { get; set; }

    public Money AmountSar { get; set; }
}
