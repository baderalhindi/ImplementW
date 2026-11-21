using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Common.Auditing;

/// <summary>
/// Client review, item 8: the one way a register record is deleted. It is stamped, so the query filter takes it out of every read, and the
/// audit event says who deleted what; nothing is removed.
/// </summary>
public static class RecordDeletion
{
    /// <summary>409: the record is in an approval that has not finished; deleting it would leave the approval with nothing to decide.</summary>
    public const string ApprovalPending = "RECORD_APPROVAL_PENDING";

    /// <summary>Stamps <paramref name="record"/> deleted by <paramref name="actorId"/> and returns the audit event to stage with the save.</summary>
    public static AuditEntry Stamp<T>(T record, string module, Guid actorId, Guid? projectId, DateTimeOffset now)
        where T : AuditedEntity, IDeletable
    {
        ArgumentNullException.ThrowIfNull(record);
        record.DeletedAt = now;
        record.DeletedBy = actorId;
        record.UpdatedAt = now;
        record.UpdatedBy = actorId;
        string type = typeof(T).Name;
        return new AuditEntry(AuditEventClass.DataChange, $"{module}.{type}Deleted", AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(module, type, record.Id),
            ScopeProjectId = projectId,
            Attributes = [AuditAttribute.Of("deleted_at", now)],
        };
    }
}
