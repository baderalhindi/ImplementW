using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Tests.Unit.Application.MasterDataConfig;

/// <summary>
/// The content rules (TASK-034): each family carries only its sections, references are PUBLISHED items of the right
/// catalogue, and validation requires the shape ADR-011, ADR-013, ADR-015, ADR-016 and ADR-004 fix. Every issue is named
/// by its path.
/// </summary>
public sealed class ConfigurationContentRulesTests
{
    private static readonly Guid Cost = Guid.Parse("00000000-0000-4000-8000-00000000d001");
    private static readonly Guid Schedule = Guid.Parse("00000000-0000-4000-8000-00000000d002");
    private static readonly Guid Light = Guid.Parse("00000000-0000-4000-8000-00000000e001");
    private static readonly Guid Full = Guid.Parse("00000000-0000-4000-8000-00000000e002");
    private static readonly Guid DraftProfile = Guid.Parse("00000000-0000-4000-8000-00000000e003");
    private static readonly Guid Controlled = Guid.Parse("00000000-0000-4000-8000-00000000e101");
    private static readonly Guid Photo = Guid.Parse("00000000-0000-4000-8000-00000000e201");
    private static readonly Guid Role = Guid.Parse("00000000-0000-4000-8000-000000000001");

    private static readonly ConfigurationReferences References = new(
        new Dictionary<Guid, ItemFacts>
        {
            [Cost] = new(Cost, MasterDataCatalogueCodes.ImpactDimension, GovernedLifecycleState.Published),
            [Schedule] = new(Schedule, MasterDataCatalogueCodes.ImpactDimension, GovernedLifecycleState.Published),
            [Light] = new(Light, MasterDataCatalogueCodes.GovernanceProfile, GovernedLifecycleState.Published),
            [Full] = new(Full, MasterDataCatalogueCodes.GovernanceProfile, GovernedLifecycleState.Published),
            [DraftProfile] = new(DraftProfile, MasterDataCatalogueCodes.GovernanceProfile, GovernedLifecycleState.Draft),
            [Controlled] = new(Controlled, MasterDataCatalogueCodes.DocumentControlLevel, GovernedLifecycleState.Published),
            [Photo] = new(Photo, MasterDataCatalogueCodes.ContributionType, GovernedLifecycleState.Published),
        },
        new Dictionary<Guid, GovernedLifecycleState>(),
        new HashSet<Guid> { Role },
        new Dictionary<string, IReadOnlyList<Guid>>
        {
            [MasterDataCatalogueCodes.ImpactDimension] = [Cost, Schedule],
            [MasterDataCatalogueCodes.GovernanceProfile] = [Light, Full],
            [MasterDataCatalogueCodes.ContributionType] = [Photo],
        });

    [Fact]
    public void AFamilyCarriesOnlyItsOwnSections()
    {
        ConfigurationContent content = new()
        {
            Values = [new ConfigurationValueEntry("UPDATE_CADENCE_DAYS", ConfigurationValueType.Integer, "7")],
            RiskRatings = [new RiskRatingEntry("HIGH", Label("High"), 3)],
        };

        Assert.Equal([new FieldIssue("riskRatings", ContentIssueCodes.SectionNotAllowed)], Entries(ConfigurationFamilyCodes.WorkflowPolicy, content));
        Assert.Empty(Entries(ConfigurationFamilyCodes.RiskMatrix, content));
    }

    [Fact]
    public void AnUnknownFamilyCarriesNothing() =>
        Assert.Contains(new FieldIssue("values", ContentIssueCodes.SectionNotAllowed),
            Entries("NOT_A_FAMILY", new ConfigurationContent { Values = [new ConfigurationValueEntry("A", ConfigurationValueType.Text, "x")] }));

    [Fact]
    public void AReferenceMustBeAPublishedItemOfTheRightCatalogue()
    {
        ConfigurationContent content = new()
        {
            MaterialityBands = [Band(Cost, 1), Band(DraftProfile, 1), Band(Guid.NewGuid(), 1)],
        };

        Assert.Equal(
            [
                new FieldIssue("materialityBands[0].governanceProfileItemId", ContentIssueCodes.WrongCatalogue),
                new FieldIssue("materialityBands[1].governanceProfileItemId", ContentIssueCodes.NotPublished),
                new FieldIssue("materialityBands[2].governanceProfileItemId", FieldIssue.NotFound),
            ],
            Entries(ConfigurationFamilyCodes.MaterialityBand, content));
    }

    [Fact]
    public void ValuesAreWellFormedUniqueAndInRange()
    {
        ConfigurationContent content = new()
        {
            Values =
            [
                new ConfigurationValueEntry("lower_case", ConfigurationValueType.Text, "x"),
                new ConfigurationValueEntry("CADENCE", ConfigurationValueType.Integer, "seven"),
                new ConfigurationValueEntry("TOLERANCE", ConfigurationValueType.Percent, "101"),
                new ConfigurationValueEntry("CADENCE", ConfigurationValueType.DurationDays, "-1"),
            ],
        };

        Assert.Equal(
            [
                new FieldIssue("values[0].key", ContentIssueCodes.Malformed),
                new FieldIssue("values[1].value", ContentIssueCodes.Malformed),
                new FieldIssue("values[2].value", ContentIssueCodes.OutOfRange),
                new FieldIssue("values[3].key", FieldIssue.Duplicate),
                new FieldIssue("values[3].value", ContentIssueCodes.Malformed),
            ],
            Entries(ConfigurationFamilyCodes.WorkflowPolicy, content));
    }

    [Fact]
    public void AMatrixCellNamesARatingOfItsOwnVersion() =>
        Assert.Equal(
            [new FieldIssue("riskMatrixCells[0].ratingCode", FieldIssue.NotFound)],
            Entries(ConfigurationFamilyCodes.RiskMatrix, new ConfigurationContent { RiskMatrixCells = [new RiskMatrixCellEntry(1, 1, "LOW")] }));

    [Fact]
    public void ANotificationRecipientIsARole() =>
        Assert.Equal(
            [new FieldIssue("notificationEventFamilies[0].recipientRoleIds[1]", FieldIssue.NotFound)],
            Entries(ConfigurationFamilyCodes.NotificationRouting, new ConfigurationContent
            {
                NotificationEventFamilies = [new NotificationEventFamilyEntry("SECURITY", Label("Security"), true, [new NotificationChannelEntry(NotificationChannel.Sms, true)], [Role, Guid.NewGuid()])],
            }));

    [Fact]
    public void EmptyContentIsNeverValidated() =>
        Assert.Equal([new FieldIssue("content", ContentIssueCodes.Incomplete)], Complete(ConfigurationFamilyCodes.WorkflowPolicy, new ConfigurationContent()));

    /// <summary>ADR-011: five probability levels, five levels for every impact dimension, at least one rating, all 25 cells.</summary>
    [Fact]
    public void ARiskMatrixIsCompleteOnlyWithEveryLevelAndCell()
    {
        ConfigurationContent levelsOnly = new()
        {
            ProbabilityLevels = [.. Levels().Select(l => new ProbabilityLevelEntry(l, Label($"P{l}"), null, null))],
            ImpactLevels = [.. Levels().Select(l => new ImpactLevelEntry(Cost, l, Label($"I{l}"), null, null, null))],
        };
        ConfigurationContent complete = levelsOnly with
        {
            ImpactLevels = [.. levelsOnly.ImpactLevels, .. Levels().Select(l => new ImpactLevelEntry(Schedule, l, Label($"S{l}"), null, null, null))],
            RiskRatings = [new RiskRatingEntry("HIGH", Label("High"), 1)],
            RiskMatrixCells = [.. Levels().SelectMany(p => Levels().Select(i => new RiskMatrixCellEntry(p, i, "HIGH")))],
        };

        Assert.Equal(
            [
                new FieldIssue("impactLevels", ContentIssueCodes.Incomplete),
                new FieldIssue("riskRatings", ContentIssueCodes.Incomplete),
                new FieldIssue("riskMatrixCells", ContentIssueCodes.Incomplete),
            ],
            Complete(ConfigurationFamilyCodes.RiskMatrix, levelsOnly));
        Assert.Empty(Entries(ConfigurationFamilyCodes.RiskMatrix, complete));
        Assert.Empty(Complete(ConfigurationFamilyCodes.RiskMatrix, complete));
    }

    /// <summary>ADR-015 and ADR-016: every published governance profile has its settings and its three bands.</summary>
    [Fact]
    public void EveryGovernanceProfileHasItsSettingsAndThreeBands()
    {
        ConfigurationContent oneProfile = new() { GovernanceProfiles = [Profile(Light)] };
        ConfigurationContent twoBands = new() { MaterialityBands = [Band(Light, 1), Band(Light, 2), Band(Full, 1), Band(Full, 2), Band(Full, 3)] };

        Assert.Equal([new FieldIssue("governanceProfiles", ContentIssueCodes.Incomplete)], Complete(ConfigurationFamilyCodes.GovernanceProfile, oneProfile));
        Assert.Empty(Complete(ConfigurationFamilyCodes.GovernanceProfile, new ConfigurationContent { GovernanceProfiles = [Profile(Light), Profile(Full)] }));
        Assert.Equal([new FieldIssue("materialityBands", ContentIssueCodes.Incomplete)], Complete(ConfigurationFamilyCodes.MaterialityBand, twoBands));
    }

    /// <summary>ADR-013: every contribution type has a rule in both participation modes.</summary>
    [Fact]
    public void EveryContributionTypeHasARuleInBothModes()
    {
        ConfigurationContent entityOnly = new() { ParticipationRules = [new ParticipationRuleEntry(ParticipationMode.EntityManaged, Photo, true)] };

        Assert.Equal([new FieldIssue("participationRules", ContentIssueCodes.Incomplete)], Complete(ConfigurationFamilyCodes.Participation, entityOnly));
        Assert.Empty(Complete(ConfigurationFamilyCodes.Participation, entityOnly with
        {
            ParticipationRules = [.. entityOnly.ParticipationRules, new ParticipationRuleEntry(ParticipationMode.AhdaManaged, Photo, false)],
        }));
    }

    /// <summary>ADR-004: a Mandatory family (the recipient cannot turn it off) must reach someone on some channel.</summary>
    [Fact]
    public void AMandatoryEventFamilyReachesSomeone()
    {
        ConfigurationContent content = new()
        {
            NotificationEventFamilies =
            [
                new NotificationEventFamilyEntry("SECURITY", Label("Security"), true, [new NotificationChannelEntry(NotificationChannel.Sms, false)], [Role]),
                new NotificationEventFamilyEntry("DIGEST", Label("Digest"), false, [], []),
            ],
        };

        Assert.Equal([new FieldIssue("notificationEventFamilies[0]", ContentIssueCodes.Incomplete)], Complete(ConfigurationFamilyCodes.NotificationRouting, content));
    }

    private static List<FieldIssue> Entries(string family, ConfigurationContent content) => ConfigurationContentRules.CheckEntries(family, content, References);

    private static List<FieldIssue> Complete(string family, ConfigurationContent content) => ConfigurationContentRules.CheckComplete(family, content, References);

    private static BilingualLabel Label(string name) => new($"اسم {name}", name);

    private static short[] Levels() => [1, 2, 3, 4, 5];

    private static MaterialityBandEntry Band(Guid profile, short bandNo) => new(profile, bandNo, 5m, 1_000_000m, 10m, 30, null, bandNo > 1);

    private static GovernanceProfileEntry Profile(Guid profile) => new(profile, true, true, 3, 30, Controlled, true, null, null, ["BUDGET"]);
}
