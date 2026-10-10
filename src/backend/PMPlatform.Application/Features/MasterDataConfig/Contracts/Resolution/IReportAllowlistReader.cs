using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

/// <summary>
/// E-U2 for the SCR-138 allowlist (REPORT_RULES, ADR-019; TASK-071): the entries of the version in force with their row ids, which a saved
/// composition references (ERD §5.20: the foreign key is the allowlist's enforcement). Resolution fails closed, as every resolution does.
/// </summary>
public interface IReportAllowlistReader
{
    /// <summary>The allowlist of the REPORT_RULES version effective at <paramref name="asOf"/>.</summary>
    /// <exception cref="ConfigurationMissingException">No version, or more than one, is effective at that moment.</exception>
    public Task<ReportAllowlist> ResolveAsync(DateTimeOffset asOf, CancellationToken cancellationToken);

    /// <summary>The entries named, of whatever version: what a saved composition was made of.</summary>
    public Task<IReadOnlyList<ReportAllowlistEntryReference>> FindAsync(IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken);
}

/// <summary>A REPORT_RULES version's allowlist.</summary>
public sealed record ReportAllowlist(Guid ConfigurationVersionId, int VersionNo, IReadOnlyList<ReportAllowlistEntryReference> Entries);

/// <summary>An allowlist entry with its row id: a field the explorer may expose, whether it may filter and sort by it, and its classification (ADR-010).</summary>
public sealed record ReportAllowlistEntryReference(
    Guid Id, Guid ConfigurationVersionId, string SourceEntityCode, string FieldCode, BilingualLabel Label, bool IsFilterable, bool IsSortable, Guid? DataClassificationItemId);
