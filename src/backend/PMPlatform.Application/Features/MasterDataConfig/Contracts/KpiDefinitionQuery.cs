using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>By code. <see cref="Text"/> matches code or either name, ignoring case; an empty <see cref="LifecycleStates"/> is no filter.</summary>
public sealed record KpiDefinitionQuery(IReadOnlyCollection<GovernedLifecycleState> LifecycleStates, string? Text, PageRequest Page);
