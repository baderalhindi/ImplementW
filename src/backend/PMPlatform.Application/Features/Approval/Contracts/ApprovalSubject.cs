namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// What is being approved, as the source module names it (M-8): its module, aggregate type, id and business revision.
/// Approval holds no source-module type and never reads the source's data.
/// </summary>
public sealed record ApprovalSubject(string Module, string Type, Guid Id, int RevisionNo);
