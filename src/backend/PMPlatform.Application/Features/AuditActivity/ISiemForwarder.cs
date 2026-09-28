namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>Sends the audit events queued for the SIEM (PTBC-029, CTL-26). Run on a schedule by the host.</summary>
public interface ISiemForwarder
{
    /// <summary>Sends up to <paramref name="batchSize"/> queued events, oldest first; returns how many the SIEM accepted.</summary>
    public Task<int> ForwardPendingAsync(int batchSize, CancellationToken cancellationToken);
}
