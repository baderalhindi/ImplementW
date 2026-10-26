using System.Globalization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>
/// A concern's severity, computed by the server and never taken from a client (TASK-057; WF-07 §5.1, BR-ISS-008/009). The rules:
/// <list type="number">
/// <item>The impacts fit the scale of the RISK_MATRIX version in force — the dimension set and five levels risks and issues share
/// (ADR-011): each dimension at most once, each defined by the version, each at a level the version defines for it. A concern need
/// not assess every dimension: one not given is not applicable to it (VAL-ISS-006).</item>
/// <item>The overall impact is the highest level among them (BR-ISS-008), so a severe dimension is never averaged away.</item>
/// <item>The severity is the CONCERN_SEVERITY item the same version names for that overall level, in its value
/// <c>CONCERN_SEVERITY_LEVEL_{n}</c>; the item must be PUBLISHED.</item>
/// <item>The concern stores the overall level, the severity item and the version id, so a later publication changes no recorded
/// severity; the next assessment is computed under, and pins, the version in force then.</item>
/// </list>
/// Generic (OQ-006): no level, dimension, label or mapping is named in code. A missing version, mapping or item fails closed:
/// <see cref="ConfigurationMissingException"/>, 422 CONFIGURATION_MISSING.
/// </summary>
internal sealed class ConcernSeverity(IConfigurationResolver configuration, IMasterDataResolver masterData)
{
    public const string LevelKeyPrefix = "CONCERN_SEVERITY_LEVEL_";

    /// <summary>The severity of <paramref name="impacts"/> under the version in force at <paramref name="asOf"/>, or the refusal.</summary>
    public async Task<AdministrationResult<ComputedSeverity>> ComputeAsync(IReadOnlyList<ConcernImpactInput> impacts, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(impacts);
        ResolvedConfiguration scale = await configuration.ResolveAsync(ConfigurationFamilyCodes.RiskMatrix, asOf, cancellationToken).ConfigureAwait(false);
        if (Check(scale.Content, impacts) is { Count: > 0 } issues)
        {
            return AdministrationError.Rule(ConcernErrorCodes.ImpactInvalid, [.. issues]);
        }

        short overall = OverallImpactOf(impacts);
        string code = SeverityCodeOf(scale, overall);
        MasterDataItemReference severity = (await masterData.ListPublishedItemsAsync(MasterDataCatalogueCodes.ConcernSeverity, cancellationToken).ConfigureAwait(false))
                                           .SingleOrDefault(i => i.Code == code)
                                           ?? throw new ConfigurationMissingException(ConfigurationFamilyCodes.RiskMatrix, ConfigurationMissingReason.EntryInvalid, $"concern severity {code}");
        return new ComputedSeverity(overall, severity.Id, severity.Code, scale.VersionId);
    }

    /// <summary>Every way <paramref name="impacts"/> misses the scale: a dimension it does not define, one given twice, a level it does not define. Empty when they fit.</summary>
    public static IReadOnlyList<FieldIssue> Check(ConfigurationContent scale, IReadOnlyList<ConcernImpactInput> impacts)
    {
        ArgumentNullException.ThrowIfNull(scale);
        ArgumentNullException.ThrowIfNull(impacts);

        ILookup<Guid, short> levels = scale.ImpactLevels.ToLookup(l => l.ImpactDimensionItemId, l => l.Level);
        List<FieldIssue> issues = [];
        HashSet<Guid> seen = [];
        for (int i = 0; i < impacts.Count; i++)
        {
            ConcernImpactInput impact = impacts[i];
            if (!levels.Contains(impact.ImpactDimensionItemId))
            {
                issues.Add(new FieldIssue($"impacts[{i}].impactDimensionItemId", FieldIssue.NotAllowed));
            }
            else if (!seen.Add(impact.ImpactDimensionItemId))
            {
                issues.Add(new FieldIssue($"impacts[{i}].impactDimensionItemId", FieldIssue.Duplicate));
            }
            else if (!levels[impact.ImpactDimensionItemId].Contains(impact.ImpactLevel))
            {
                issues.Add(new FieldIssue($"impacts[{i}].impactLevel", FieldIssue.NotAllowed));
            }
        }

        return issues;
    }

    /// <summary>The overall impact: the highest level of any dimension assessed.</summary>
    public static short OverallImpactOf(IEnumerable<ConcernImpactInput> impacts) => impacts.Max(i => i.ImpactLevel);

    /// <summary>The CONCERN_SEVERITY code the version names for an overall level.</summary>
    public static string SeverityCodeOf(ResolvedConfiguration scale, short overallImpactLevel)
    {
        ArgumentNullException.ThrowIfNull(scale);
        return scale.RequireText(LevelKeyPrefix + overallImpactLevel.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>A computed severity: the overall level, the CONCERN_SEVERITY item and its code, and the version whose rule computed it.</summary>
internal sealed record ComputedSeverity(short OverallImpactLevel, Guid SeverityItemId, string SeverityCode, Guid ConfigurationVersionId);
