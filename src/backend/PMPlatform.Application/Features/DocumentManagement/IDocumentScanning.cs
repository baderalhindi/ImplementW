namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>One pass of the malware scan over SCAN_PENDING versions, run by the scan worker.</summary>
public interface IDocumentScanning
{
    /// <summary>Scans up to <paramref name="batchSize"/> versions; returns how many received a verdict.</summary>
    public Task<int> RunAsync(int batchSize, CancellationToken cancellationToken);
}
