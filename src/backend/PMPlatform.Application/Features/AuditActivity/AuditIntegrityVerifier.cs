using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// Walks the whole hash chain (AUD-CC-27, BR-AUD-047): every event's hash must be the hash of its row as it is now, its previous
/// hash must be its predecessor's, and its attributes must still have the digest stored with it. A change, a deletion or an
/// insertion out of band is found at the event it touched or the one after it. It reports and repairs nothing.
/// </summary>
internal sealed class AuditIntegrityVerifier(IAuditIntegrityRepository repository)
{
    internal const int BatchSize = 1000;

    public async Task<AuditIntegrityReport> VerifyAsync(CancellationToken cancellationToken)
    {
        int verified = 0, brokenLinks = 0, digestMismatches = 0, withoutDigest = 0;
        List<AuditIntegrityFinding> findings = [];
        string? predecessorHash = null;
        AuditStorePosition? after = null;
        while (true)
        {
            IReadOnlyList<AuditChainLink> links = await repository.ReadChainAsync(after, BatchSize, cancellationToken).ConfigureAwait(false);
            if (links.Count == 0)
            {
                return new AuditIntegrityReport(verified, brokenLinks, digestMismatches, withoutDigest, findings);
            }

            ILookup<Guid, AuditEventAttribute> attributes = (await repository.ReadAttributesAsync([.. links.Where(l => l.AttributeDigest is not null).Select(l => l.AuditEventId)], cancellationToken)
                .ConfigureAwait(false)).ToLookup(a => a.AuditEventId);
            foreach (AuditChainLink link in links)
            {
                verified++;
                if (!string.Equals(link.EventHash, link.RecomputedHash, StringComparison.Ordinal) || !string.Equals(link.PreviousEventHash, predecessorHash, StringComparison.Ordinal))
                {
                    brokenLinks++;
                    Add(findings, link.AuditEventId, AuditIntegrityFindingKind.BrokenLink);
                }

                if (link.AttributeDigest is null)
                {
                    withoutDigest++;
                }
                else if (!string.Equals(link.AttributeDigest, AuditAttributeDigest.Of(attributes[link.AuditEventId]), StringComparison.Ordinal))
                {
                    digestMismatches++;
                    Add(findings, link.AuditEventId, AuditIntegrityFindingKind.AttributeDigestMismatch);
                }

                predecessorHash = link.EventHash;
            }

            after = new AuditStorePosition(links[^1].RecordedAt, links[^1].AuditEventId);
        }
    }

    private static void Add(List<AuditIntegrityFinding> findings, Guid auditEventId, AuditIntegrityFindingKind kind)
    {
        if (findings.Count < AuditIntegrityReport.MaxListedFindings)
        {
            findings.Add(new AuditIntegrityFinding(auditEventId, kind));
        }
    }
}
