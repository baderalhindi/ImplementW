namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>The <c>audit_activity</c> rows as the trail writes them.</summary>
public interface IAuditEventRepository
{
    /// <summary>Adds the record to the request's unit of work, committed by the producer's next save.</summary>
    public void Stage(AuditRecord record);

    /// <summary>Commits the record now in a unit of work of its own, independent of anything the request has pending.</summary>
    public Task AppendAsync(AuditRecord record);
}
