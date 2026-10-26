using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.FinancialKpi;

/// <summary>
/// An update's actual expenditure in one Etimad cost category (ADR-008 extension). Launch is project total only, so no operation
/// writes one yet (financial-kpi.md D-4). Delete policy: CASCADE.
/// </summary>
public sealed class FinancialProgressUpdateLine : AuditedEntity
{
    public Guid FinancialProgressUpdateId { get; set; }

    public Guid EtimadCategoryItemId { get; set; }

    public Money ActualSar { get; set; }
}
