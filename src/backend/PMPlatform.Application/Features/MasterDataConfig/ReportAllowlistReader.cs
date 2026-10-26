using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>The REPORT_RULES allowlist with row ids (TASK-071), resolved by <see cref="IConfigurationResolver"/>'s rule: by date alone, failing closed.</summary>
internal sealed class ReportAllowlistReader(IConfigurationResolver resolver, IConfigurationRepository configuration) : IReportAllowlistReader
{
    public async Task<ReportAllowlist> ResolveAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        ResolvedConfiguration resolved = await resolver.ResolveAsync(ConfigurationFamilyCodes.ReportRules, asOf, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ReportAllowlistEntry> rows = await configuration.ListReportAllowlistAsync([resolved.VersionId], null, cancellationToken).ConfigureAwait(false);
        return new ReportAllowlist(resolved.VersionId, resolved.VersionNo, [.. rows.OrderBy(r => r.SourceEntityCode, StringComparer.Ordinal).ThenBy(r => r.FieldCode, StringComparer.Ordinal).Select(Reference)]);
    }

    public async Task<IReadOnlyList<ReportAllowlistEntryReference>> FindAsync(IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken) =>
        entryIds.Count == 0 ? [] : [.. (await configuration.ListReportAllowlistAsync(null, entryIds, cancellationToken).ConfigureAwait(false)).Select(Reference)];

    private static ReportAllowlistEntryReference Reference(ReportAllowlistEntry row) =>
        new(row.Id, row.ConfigurationVersionId, row.SourceEntityCode, row.FieldCode, row.Label, row.IsFilterable, row.IsSortable, row.DataClassificationItemId);
}
