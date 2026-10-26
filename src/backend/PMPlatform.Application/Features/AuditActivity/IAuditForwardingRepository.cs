namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>The <c>audit_forwarding_record</c> rows the SIEM forwarder reads and updates.</summary>
public interface IAuditForwardingRepository
{
    /// <summary>Up to <paramref name="count"/> PENDING or FAILED records, oldest first, tracked for update.</summary>
    public Task<IReadOnlyList<PendingForwarding>> FindUnforwardedAsync(int count, CancellationToken cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken);
}
