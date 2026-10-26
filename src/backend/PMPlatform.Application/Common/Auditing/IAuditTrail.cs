namespace PMPlatform.Application.Common.Auditing;

/// <summary>
/// Where every module records its formal audit events (FG-06, PTBC-011; ADR-003 E-U3). A producer knows only this
/// interface and <see cref="AuditEntry"/>; the store, its hash chain and SIEM forwarding are AuditActivity's.
/// </summary>
public interface IAuditTrail
{
    /// <summary>
    /// Adds the event to the unit of work that the producer's next save commits, so the change and its audit event are
    /// committed together or not at all. Use it for a change the producer is about to save.
    /// </summary>
    public void Stage(AuditEntry entry);

    /// <summary>
    /// Commits the event now, in its own transaction. Use it for an occurrence that changes nothing, such as a refused
    /// sign-in or a denied request. It takes no cancellation token, so a caller who aborts the request is still audited.
    /// If the event cannot be committed, the method throws and the request fails rather than go unaudited.
    /// </summary>
    public Task RecordAsync(AuditEntry entry);
}
