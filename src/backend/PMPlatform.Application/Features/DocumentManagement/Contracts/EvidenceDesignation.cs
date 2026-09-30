namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>Designate a link as evidence of <see cref="EvidenceTypeItemId"/>, pinned to <see cref="DocumentVersionId"/>.</summary>
public sealed record EvidenceDesignation(Guid BusinessLinkId, Guid DocumentVersionId, Guid EvidenceTypeItemId);
