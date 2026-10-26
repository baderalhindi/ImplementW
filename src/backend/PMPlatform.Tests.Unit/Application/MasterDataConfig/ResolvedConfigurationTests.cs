using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Tests.Unit.Application.MasterDataConfig;

/// <summary>
/// TASK-034 acceptance criterion 3: a missing required configuration value fails the requesting operation explicitly
/// rather than silently defaulting (Blueprint Section 12).
/// </summary>
public sealed class ResolvedConfigurationTests
{
    private static readonly Guid Profile = Guid.Parse("00000000-0000-4000-8000-00000000f001");
    private static readonly Guid ContributionType = Guid.Parse("00000000-0000-4000-8000-00000000f002");

    private static readonly ResolvedConfiguration Resolved = new(
        Guid.NewGuid(), "WORKFLOW_POLICY", 3, DateTimeOffset.UnixEpoch, null,
        new ConfigurationContent
        {
            Values =
            [
                new ConfigurationValueEntry("REMINDER_OFFSET_DAYS", ConfigurationValueType.DurationDays, "5"),
                new ConfigurationValueEntry("OVERRIDE_TOLERANCE", ConfigurationValueType.Percent, "2.5"),
                new ConfigurationValueEntry("ESCALATE", ConfigurationValueType.Boolean, "true"),
            ],
            ParticipationRules = [new ParticipationRuleEntry(ParticipationMode.EntityManaged, ContributionType, IsEnabled: false)],
            MaterialityBands = [Band(1), Band(2)],
        });

    [Fact]
    public void PresentValuesAreReadAsTheirType()
    {
        Assert.Equal(5, Resolved.RequireDurationDays("REMINDER_OFFSET_DAYS"));
        Assert.Equal(2.5m, Resolved.RequirePercent("OVERRIDE_TOLERANCE"));
        Assert.True(Resolved.RequireBoolean("ESCALATE"));
        Assert.False(Resolved.IsContributionEnabled(ParticipationMode.EntityManaged, ContributionType));
    }

    [Fact]
    public void AMissingValueThrowsInsteadOfDefaulting()
    {
        ConfigurationMissingException missing = Assert.Throws<ConfigurationMissingException>(() => Resolved.RequireInteger("FRESHNESS_THRESHOLD_DAYS"));

        Assert.Equal(("WORKFLOW_POLICY", ConfigurationMissingReason.EntryMissing, "value FRESHNESS_THRESHOLD_DAYS"), (missing.ConfigurationCode, missing.Reason, missing.Entry));
    }

    [Fact]
    public void AValueReadAsAnotherTypeIsInvalidNotConverted() =>
        Assert.Equal(ConfigurationMissingReason.EntryInvalid, Assert.Throws<ConfigurationMissingException>(() => Resolved.RequireInteger("OVERRIDE_TOLERANCE")).Reason);

    /// <summary>ADR-013: a contribution type with no rule in a mode is not "disabled"; the operation cannot know, so it fails.</summary>
    [Fact]
    public void AMissingParticipationRuleIsNotReadAsDisabled() =>
        Assert.Throws<ConfigurationMissingException>(() => Resolved.IsContributionEnabled(ParticipationMode.AhdaManaged, ContributionType));

    /// <summary>ADR-016: a profile's bands are all three or none.</summary>
    [Fact]
    public void TwoOfThreeMaterialityBandsAreMissingConfiguration() =>
        Assert.Throws<ConfigurationMissingException>(() => Resolved.RequireMaterialityBands(Profile));

    [Fact]
    public void EveryTypedLookupFailsClosed()
    {
        Assert.Throws<ConfigurationMissingException>(() => Resolved.RequireGovernanceProfile(Profile));
        Assert.Throws<ConfigurationMissingException>(() => Resolved.RequireRiskRating(3, 4));
        Assert.Throws<ConfigurationMissingException>(() => Resolved.RequireNotificationEventFamily("SECURITY_ALERT"));
        Assert.Throws<ConfigurationMissingException>(() => Resolved.RequireKpiPolicy(Guid.NewGuid()));
    }

    private static MaterialityBandEntry Band(short bandNo) => new(Profile, bandNo, 5m, null, null, null, null, bandNo > 1);
}
