using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>Newest version first within a family. An empty <see cref="LifecycleStates"/> is no filter.</summary>
public sealed record ConfigurationVersionQuery(Guid? FamilyId, IReadOnlyCollection<GovernedLifecycleState> LifecycleStates, PageRequest Page);
