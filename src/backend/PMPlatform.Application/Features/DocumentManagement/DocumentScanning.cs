using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>
/// The malware scan (CTL-20). A pending version's bytes go to the scanner and its verdict becomes the version's final scan
/// state: CLEAN, QUARANTINED (malware found) or SCAN_FAILED (the scanner could not decide). Without a scanner nothing is
/// scanned, so nothing becomes CLEAN: the check fails closed. A version the scanner could not be asked about stays
/// pending and goes to the back of the queue.
/// </summary>
internal sealed partial class DocumentScanning(
    IDocumentRepository repository,
    IDocumentStorage storage,
    IMalwareScanner scanner,
    DocumentAccess access,
    IAuditTrail audit,
    TimeProvider timeProvider,
    ILogger<DocumentScanning> logger) : IDocumentScanning
{
    public const int ScanReferenceLength = 200;

    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (!scanner.IsConfigured || !storage.IsConfigured)
        {
            return 0;
        }

        int decided = 0;
        foreach (Guid versionId in await repository.FindPendingScanIdsAsync(batchSize, cancellationToken).ConfigureAwait(false))
        {
            DocumentVersion? version = await repository.FindVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
            if (version is not { ScanState: ScanState.ScanPending })
            {
                continue;
            }

            MalwareScanResult? result = await TryScanAsync(version, cancellationToken).ConfigureAwait(false);
            DateTimeOffset now = timeProvider.GetUtcNow();
            version.UpdatedAt = now;
            version.UpdatedBy = DocumentServicePrincipal.Id;
            if (result is not null)
            {
                version.ScanState = result.Verdict switch
                {
                    MalwareVerdict.Clean => ScanState.Clean,
                    MalwareVerdict.Infected => ScanState.Quarantined,
                    MalwareVerdict.Unscannable => ScanState.ScanFailed,
                    _ => throw new InvalidOperationException($"Unknown malware verdict {result.Verdict}."),
                };
                version.ScanCompletedAt = now;
                version.ScanReference = result.Reference is { Length: > ScanReferenceLength } reference ? reference[..ScanReferenceLength] : result.Reference;

                Document document = (await repository.FindDocumentAsync(version.DocumentId, null, cancellationToken).ConfigureAwait(false))!;
                audit.Stage(DocumentAudit.ScanCompleted(document, await access.AnchorsOfAsync(document, cancellationToken).ConfigureAwait(false), version));
            }

            // Without a verdict only updated_at moves, so the version is retried after the others that are waiting.
            if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == DocumentSaveOutcome.Saved && result is not null)
            {
                decided++;
            }
        }

        return decided;
    }

    private async Task<MalwareScanResult?> TryScanAsync(DocumentVersion version, CancellationToken cancellationToken)
    {
        try
        {
            Stream content = await storage.OpenReadAsync(version.StorageObjectKey, cancellationToken).ConfigureAwait(false);
            await using (content.ConfigureAwait(false))
            {
                return await scanner.ScanAsync(content, version.FileName, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A store or scanner outage leaves the version pending; the next pass asks again.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogScanNotObtained(logger, version.Id, exception.GetType().Name);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No malware verdict for document version {VersionId}: {Reason}. It stays SCAN_PENDING.")]
    private static partial void LogScanNotObtained(ILogger logger, Guid versionId, string reason);
}
