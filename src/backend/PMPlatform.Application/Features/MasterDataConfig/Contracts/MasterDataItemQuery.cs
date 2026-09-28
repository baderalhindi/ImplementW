using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>
/// ADM-020–029 filters, in display order: by catalogue, then sort order (indexing-strategy I-09). <see cref="Text"/> matches code or either label,
/// ignoring case. An empty <see cref="LifecycleStates"/> is no filter.
/// </summary>
public sealed record MasterDataItemQuery(
    Guid? CatalogueId, IReadOnlyCollection<GovernedLifecycleState> LifecycleStates, Guid? ParentItemId, string? Text, PageRequest Page);
