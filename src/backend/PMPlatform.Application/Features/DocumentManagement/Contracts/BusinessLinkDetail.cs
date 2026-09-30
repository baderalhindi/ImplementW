using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>A link and the evidence designated on it, ended links included: unlinking never deletes (TASK-037).</summary>
public sealed record BusinessLinkDetail(
    Guid Id,
    Guid DocumentId,
    BusinessLinkRole LinkRole,
    BusinessTarget Target,
    Guid LinkedByUserId,
    DateTimeOffset LinkedAt,
    DateTimeOffset? UnlinkedAt,
    Guid? UnlinkedByUserId,
    IReadOnlyList<EvidenceReferenceDetail> Evidence);
