namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>
/// What a document's authorization is anchored on besides its own owner and classification: its project, and the project's
/// owning department, delivering entity (ADR-013) and owner — its Project Manager, or its registrant while it has none. All null for a
/// library document. <see cref="ProjectClosed"/>: the project is CLOSED, terminal and read-only (WF-10, TASK-063), so its documents take no write.
/// </summary>
public sealed record DocumentAnchors(Guid? ProjectId, Guid? DepartmentId, Guid? ExternalEntityId, bool ProjectClosed = false, Guid? ProjectOwnerUserId = null)
{
    public static DocumentAnchors Library { get; } = new(null, null, null);
}
