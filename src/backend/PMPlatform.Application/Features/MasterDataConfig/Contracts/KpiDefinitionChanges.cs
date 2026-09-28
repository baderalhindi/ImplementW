using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>
/// The editable representation of a KPI definition. While DRAFT its author may change all of it; once PUBLISHED only
/// the name and description are corrected, because the unit and direction are what recorded values mean.
/// </summary>
public sealed record KpiDefinitionChanges(BilingualLabel Name, BilingualLabel? Description, Guid UnitItemId, KpiDirection Direction);
