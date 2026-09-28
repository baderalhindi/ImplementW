using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>A controlled list of ADM-020–029. The catalogues are the platform's (db/seed); only the name is AHDA's to edit.</summary>
public sealed record MasterDataCatalogueDetail(
    Guid Id,
    string Code,
    BilingualLabel Name,
    bool AllowsHierarchy,
    bool IsSystem,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);
