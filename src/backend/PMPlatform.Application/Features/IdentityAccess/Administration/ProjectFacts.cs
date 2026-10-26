namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>What a per-project assignment needs to know of its project: who delivers it and whether it is closed (ADR-013).</summary>
public sealed record ProjectFacts(Guid? ExternalEntityId, bool IsClosed);
