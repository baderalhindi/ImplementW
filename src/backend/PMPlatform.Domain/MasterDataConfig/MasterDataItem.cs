using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>
/// A stable controlled value that domain rows reference forever, with mandatory bilingual labels (ADR-012).
/// Retired, never deleted, never versioned: a label correction is an audited edit. Delete policy: RETAIN.
/// </summary>
public sealed class MasterDataItem : GovernedEntity
{
    public Guid CatalogueId { get; set; }

    /// <summary>Unique within its catalogue.</summary>
    public required string Code { get; set; }

    public required BilingualLabel Label { get; set; }

    public BilingualLabel? Description { get; set; }

    /// <summary>Set only when the catalogue allows a hierarchy.</summary>
    public Guid? ParentItemId { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Where a place is on the map, for an item of a place catalogue; null when not placed.</summary>
    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public bool IsSystem { get; set; }
}
