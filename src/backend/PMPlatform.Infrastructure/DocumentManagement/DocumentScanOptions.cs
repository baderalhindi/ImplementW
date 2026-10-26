namespace PMPlatform.Infrastructure.DocumentManagement;

/// <summary>Configuration section <c>DocumentManagement:Scan</c>: how often SCAN_PENDING versions are sent to the scanner.</summary>
internal sealed class DocumentScanOptions
{
    public const string Section = "DocumentManagement:Scan";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

    public int BatchSize { get; set; } = 20;
}
