using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Reports;

/// <summary>
/// The bytes of a generated output, in the platform's private store under its opaque key (FG-02 §9.3): never reachable but through an
/// authorised download. Deleted when the output is purged. Delete policy: HARD_WORKING.
/// </summary>
public sealed class ReportOutputContent : AuditedEntity
{
    public required string StorageObjectKey { get; set; }

#pragma warning disable CA1819 // The stored bytes, read and written whole.
    public required byte[] Content { get; set; }
#pragma warning restore CA1819
}
