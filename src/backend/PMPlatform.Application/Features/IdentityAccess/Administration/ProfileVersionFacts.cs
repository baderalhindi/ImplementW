using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>What an assignment needs to know of the profile version it binds to: its state and its canonical role.</summary>
public sealed record ProfileVersionFacts(GovernedLifecycleState LifecycleState, string RoleCode, bool RoleIsExternalEligible);
