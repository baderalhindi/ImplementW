namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>
/// Configuration section <c>DocumentManagement:Upload</c>: what an upload may be. No controlled source states either value;
/// the shipped ones are the delivery team's (document-management.md F-5). The start-up check refuses an empty list or a
/// non-positive size.
/// </summary>
public sealed class DocumentUploadPolicy
{
    public const string Section = "DocumentManagement:Upload";

    /// <summary>The largest file accepted, in bytes; larger is 413 <c>PAYLOAD_TOO_LARGE</c>.</summary>
    public long MaxFileSizeBytes { get; set; }

    /// <summary>The declared media types accepted, compared without parameters and ignoring case; any other is 415.</summary>
    public IList<string> AllowedContentTypes { get; } = [];

    public bool Allows(string contentType)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        string mediaType = contentType.Split(';', 2)[0].Trim();
        return AllowedContentTypes.Any(t => string.Equals(t, mediaType, StringComparison.OrdinalIgnoreCase));
    }
}
