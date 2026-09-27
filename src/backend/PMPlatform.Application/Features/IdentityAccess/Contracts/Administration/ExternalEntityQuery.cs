using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>ADM-013 filters, ordered by code. <see cref="Text"/> matches code or either name, ignoring case.</summary>
public sealed record ExternalEntityQuery(IReadOnlyCollection<ExternalEntityStatus> Statuses, Guid? EntityTypeItemId, string? Text, PageRequest Page);
