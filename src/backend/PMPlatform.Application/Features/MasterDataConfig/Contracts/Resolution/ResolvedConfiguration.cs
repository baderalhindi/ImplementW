using System.Globalization;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

/// <summary>
/// The content of the version that resolution chose, with the id a record pins (ERD D-13). Each <c>Require…</c>
/// returns the entry or throws <see cref="ConfigurationMissingException"/>: there is no default to fall back on.
/// </summary>
public sealed record ResolvedConfiguration(
    Guid VersionId, string FamilyCode, int VersionNo, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, ConfigurationContent Content)
{
    public ConfigurationValueEntry RequireValue(string key) =>
        Content.Values.SingleOrDefault(v => v.Key == key) ?? throw Missing($"value {key}");

    public long RequireInteger(string key) =>
        long.TryParse(Read(key, ConfigurationValueType.Integer), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value)
            ? value
            : throw Invalid($"value {key}");

    public int RequireDurationDays(string key) =>
        int.TryParse(Read(key, ConfigurationValueType.DurationDays), NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw Invalid($"value {key}");

    public decimal RequireDecimal(string key) =>
        decimal.TryParse(Read(key, ConfigurationValueType.Decimal), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : throw Invalid($"value {key}");

    public decimal RequirePercent(string key) =>
        decimal.TryParse(Read(key, ConfigurationValueType.Percent), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : throw Invalid($"value {key}");

    public bool RequireBoolean(string key) =>
        Read(key, ConfigurationValueType.Boolean) switch
        {
            "true" => true,
            "false" => false,
            _ => throw Invalid($"value {key}"),
        };

    public string RequireText(string key) => Read(key, ConfigurationValueType.Text);

    /// <summary>ADR-015: the switches of one governance profile.</summary>
    public GovernanceProfileEntry RequireGovernanceProfile(Guid governanceProfileItemId) =>
        Content.GovernanceProfiles.SingleOrDefault(p => p.GovernanceProfileItemId == governanceProfileItemId)
        ?? throw Missing($"governance profile {governanceProfileItemId}");

    /// <summary>ADR-016: the three bands of a governance profile, band 1 first.</summary>
    public IReadOnlyList<MaterialityBandEntry> RequireMaterialityBands(Guid governanceProfileItemId)
    {
        List<MaterialityBandEntry> bands = [.. Content.MaterialityBands.Where(b => b.GovernanceProfileItemId == governanceProfileItemId).OrderBy(b => b.BandNo)];
        return bands.Select(b => (int)b.BandNo).SequenceEqual([1, 2, 3]) ? bands : throw Missing($"materiality bands of {governanceProfileItemId}");
    }

    /// <summary>ADR-011: the rating of a probability and overall-impact level.</summary>
    public RiskRatingEntry RequireRiskRating(short probabilityLevel, short impactLevel)
    {
        RiskMatrixCellEntry cell = Content.RiskMatrixCells.SingleOrDefault(c => c.ProbabilityLevel == probabilityLevel && c.ImpactLevel == impactLevel)
                                   ?? throw Missing($"risk matrix cell {probabilityLevel}x{impactLevel}");
        return Content.RiskRatings.SingleOrDefault(r => r.Code == cell.RatingCode) ?? throw Invalid($"risk rating {cell.RatingCode}");
    }

    /// <summary>ADR-004: an event family with its channels, recipient roles and whether a recipient may turn it off.</summary>
    public NotificationEventFamilyEntry RequireNotificationEventFamily(string code) =>
        Content.NotificationEventFamilies.SingleOrDefault(f => f.Code == code) ?? throw Missing($"notification event family {code}");

    /// <summary>ADR-013: whether a contribution type is enabled for a project in <paramref name="mode"/>. No rule is not "disabled": it fails.</summary>
    public bool IsContributionEnabled(ParticipationMode mode, Guid contributionTypeItemId) =>
        (Content.ParticipationRules.SingleOrDefault(r => r.ParticipationMode == mode && r.ContributionTypeItemId == contributionTypeItemId)
         ?? throw Missing($"participation rule {mode} {contributionTypeItemId}")).IsEnabled;

    public KpiPolicyEntry RequireKpiPolicy(Guid kpiDefinitionId) =>
        Content.KpiPolicies.SingleOrDefault(p => p.KpiDefinitionId == kpiDefinitionId) ?? throw Missing($"KPI policy {kpiDefinitionId}");

    private string Read(string key, ConfigurationValueType type)
    {
        ConfigurationValueEntry entry = RequireValue(key);
        return entry.Type == type ? entry.Value : throw Invalid($"value {key}");
    }

    private ConfigurationMissingException Missing(string entry) => new(FamilyCode, ConfigurationMissingReason.EntryMissing, entry);

    private ConfigurationMissingException Invalid(string entry) => new(FamilyCode, ConfigurationMissingReason.EntryInvalid, entry);
}
