namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>
/// What a document's authorization is anchored on besides its own owner and classification: its project, and the project's
/// owning department and delivering entity (ADR-013). All null for a library document. <see cref="ProjectClosed"/>: the project is
/// CLOSED, terminal and read-only (WF-10, TASK-063), so its documents take no write.
/// </summary>
public sealed record DocumentAnchors(Guid? ProjectId, Guid? DepartmentId, Guid? ExternalEntityId, bool ProjectClosed = false)
{
    public static DocumentAnchors Library { get; } = new(null, null, null);
}
