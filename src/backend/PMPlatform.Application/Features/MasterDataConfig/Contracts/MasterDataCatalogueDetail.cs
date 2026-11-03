using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>
/// A controlled list of ADM-020–029. The catalogues are the platform's (db/seed); only the name is AHDA's to edit. An item's parent is an
/// item of <see cref="ParentCatalogueId"/> when it is set — a city's region, a strategic initiative's portfolio — else of the catalogue itself.
/// </summary>
public sealed record MasterDataCatalogueDetail(
    Guid Id,
    string Code,
    BilingualLabel Name,
    bool AllowsHierarchy,
    bool IsSystem,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy,
    Guid? ParentCatalogueId = null);
