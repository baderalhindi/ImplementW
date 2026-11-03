using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>
/// A controlled list (ADM-020 to ADM-029): project classification, milestone category, risk category, impact
/// dimension, KPI unit, document type, evidence type, Etimad cost category, data classification, governance
/// profile, region and the rest. Delete policy: RETAIN.
/// </summary>
public sealed class MasterDataCatalogue : AuditedEntity
{
    public required string Code { get; set; }

    public required BilingualLabel Name { get; set; }

    public bool AllowsHierarchy { get; set; }

    /// <summary>
    /// The catalogue an item's parent belongs to, when it is another one: a city lies in a region, a strategic initiative in a strategic
    /// portfolio (the 2 Nov 2026 review). Null: a parent, if the catalogue allows a hierarchy, is an item of the same catalogue.
    /// </summary>
    public Guid? ParentCatalogueId { get; set; }

    /// <summary>Shipped catalogues cannot be retired.</summary>
    public bool IsSystem { get; set; }
}
