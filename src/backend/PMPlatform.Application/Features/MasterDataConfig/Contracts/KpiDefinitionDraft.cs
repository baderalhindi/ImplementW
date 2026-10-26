using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>A new KPI definition, created DRAFT. The code is its identity and never changes.</summary>
public sealed record KpiDefinitionDraft(string Code, BilingualLabel Name, BilingualLabel? Description, Guid UnitItemId, KpiDirection Direction);
