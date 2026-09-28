namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>AHDA's SIEM ingestion endpoint (<c>SIEM_ENDPOINT_URL</c>, <c>SIEM_API_TOKEN</c>).</summary>
public interface ISiemClient
{
    /// <summary>False until both the endpoint and the token are set; forwarding waits, and nothing is lost.</summary>
    public bool IsConfigured { get; }

    /// <summary>True if the SIEM accepted the event. A refusal or an unreachable SIEM is false, never an exception.</summary>
    public Task<bool> SendAsync(SiemEvent siemEvent, CancellationToken cancellationToken);
}
