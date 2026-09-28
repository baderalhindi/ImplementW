using System.Globalization;
using System.Text.RegularExpressions;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>
/// The rules configuration content is held to. <see cref="CheckEntries"/> runs on every write: each entry is well formed
/// and in range, unique within its version, and references a PUBLISHED item of the right catalogue, a PUBLISHED KPI
/// definition or an existing role. <see cref="CheckComplete"/> runs again at validation and publication: the content is
/// not empty and has the shape an ADR fixes for its family. Every issue is reported at once, by path, never by value.
/// </summary>
internal static partial class ConfigurationContentRules
{
    private const int CodeLength = 50;
    private const int LongCodeLength = 100;
    private const int ValueLength = 500;
    private const decimal MaxAmountSar = 9_999_999_999_999_999.99m;
    private const decimal MaxMeasure = 99_999_999_999_999.9999m;
    private static readonly short[] Levels = [1, 2, 3, 4, 5];
    private static readonly short[] Bands = [1, 2, 3];

    public static List<FieldIssue> CheckEntries(string familyCode, ConfigurationContent content, ConfigurationReferences references)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(references);

        List<FieldIssue> issues = [];
        IReadOnlySet<ConfigurationSection> allowed = ConfigurationSections.Of(familyCode);
        foreach ((ConfigurationSection section, int count) in ConfigurationSections.Counts(content))
        {
            if (count > 0 && !allowed.Contains(section))
            {
                issues.Add(new FieldIssue(PathOf(section), ContentIssueCodes.SectionNotAllowed));
            }
        }

        CheckValues(content.Values, issues);
        CheckGovernanceProfiles(content.GovernanceProfiles, references, issues);
        CheckMaterialityBands(content.MaterialityBands, references, issues);
        CheckRiskMatrix(content, references, issues);
        CheckApprovalAuthority(content.ApprovalAuthority, references, issues);
        CheckNotificationRouting(content.NotificationEventFamilies, references, issues);
        CheckKpiPolicies(content.KpiPolicies, references, issues);
        CheckParticipation(content.ParticipationRules, references, issues);
        CheckEvidence(content.EvidenceRequirements, references, issues);
        CheckFieldClassifications(content.FieldClassifications, references, issues);
        CheckReportFields(content.ReportFields, references, issues);
        return issues;
    }

    /// <summary>
    /// Completeness where a source fixes the shape: ADR-011's five probability levels, five levels per impact dimension
    /// and a full 5×5 matrix; ADR-015's settings for every governance profile; ADR-016's three bands per profile; ADR-013's
    /// rule for every contribution type in both participation modes; ADR-004's mandatory families reaching someone.
    /// </summary>
    public static List<FieldIssue> CheckComplete(string familyCode, ConfigurationContent content, ConfigurationReferences references)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(references);

        List<FieldIssue> issues = [];
        if (ConfigurationSections.Counts(content).Values.All(count => count == 0))
        {
            issues.Add(new FieldIssue("content", ContentIssueCodes.Incomplete));
            return issues;
        }

        switch (familyCode)
        {
            case ConfigurationFamilyCodes.RiskMatrix:
                RequireLevels("probabilityLevels", content.ProbabilityLevels.Select(l => l.Level), issues);
                foreach (Guid dimension in references.PublishedItemsOf(MasterDataCatalogueCodes.ImpactDimension))
                {
                    RequireLevels("impactLevels", content.ImpactLevels.Where(l => l.ImpactDimensionItemId == dimension).Select(l => l.Level), issues);
                }

                if (content.RiskRatings.Count == 0)
                {
                    issues.Add(new FieldIssue("riskRatings", ContentIssueCodes.Incomplete));
                }

                if (content.RiskMatrixCells.Select(c => (c.ProbabilityLevel, c.ImpactLevel)).Distinct().Count(c => Levels.Contains(c.ProbabilityLevel) && Levels.Contains(c.ImpactLevel)) != 25)
                {
                    issues.Add(new FieldIssue("riskMatrixCells", ContentIssueCodes.Incomplete));
                }

                break;
            case ConfigurationFamilyCodes.GovernanceProfile:
                if (references.PublishedItemsOf(MasterDataCatalogueCodes.GovernanceProfile).Any(p => content.GovernanceProfiles.All(s => s.GovernanceProfileItemId != p)))
                {
                    issues.Add(new FieldIssue("governanceProfiles", ContentIssueCodes.Incomplete));
                }

                break;
            case ConfigurationFamilyCodes.MaterialityBand:
                foreach (Guid profile in references.PublishedItemsOf(MasterDataCatalogueCodes.GovernanceProfile))
                {
                    if (!content.MaterialityBands.Where(b => b.GovernanceProfileItemId == profile).Select(b => b.BandNo).Order().SequenceEqual(Bands))
                    {
                        issues.Add(new FieldIssue("materialityBands", ContentIssueCodes.Incomplete));
                        break;
                    }
                }

                break;
            case ConfigurationFamilyCodes.Participation:
                if (references.PublishedItemsOf(MasterDataCatalogueCodes.ContributionType)
                    .Any(type => Enum.GetValues<ParticipationMode>().Any(mode => !content.ParticipationRules.Any(r => r.ContributionTypeItemId == type && r.ParticipationMode == mode))))
                {
                    issues.Add(new FieldIssue("participationRules", ContentIssueCodes.Incomplete));
                }

                break;
            case ConfigurationFamilyCodes.NotificationRouting:
                for (int i = 0; i < content.NotificationEventFamilies.Count; i++)
                {
                    NotificationEventFamilyEntry family = content.NotificationEventFamilies[i];
                    if (family.IsMandatory && (family.RecipientRoleIds.Count == 0 || !family.Channels.Any(c => c.EnabledByDefault)))
                    {
                        issues.Add(new FieldIssue($"notificationEventFamilies[{i}]", ContentIssueCodes.Incomplete));
                    }
                }

                break;
            default:
                break;
        }

        return issues;
    }

    public static string PathOf(ConfigurationSection section) => section switch
    {
        ConfigurationSection.Values => "values",
        ConfigurationSection.GovernanceProfiles => "governanceProfiles",
        ConfigurationSection.MaterialityBands => "materialityBands",
        ConfigurationSection.ProbabilityLevels => "probabilityLevels",
        ConfigurationSection.ImpactLevels => "impactLevels",
        ConfigurationSection.RiskRatings => "riskRatings",
        ConfigurationSection.RiskMatrixCells => "riskMatrixCells",
        ConfigurationSection.ApprovalAuthority => "approvalAuthority",
        ConfigurationSection.NotificationEventFamilies => "notificationEventFamilies",
        ConfigurationSection.KpiPolicies => "kpiPolicies",
        ConfigurationSection.ParticipationRules => "participationRules",
        ConfigurationSection.EvidenceRequirements => "evidenceRequirements",
        ConfigurationSection.FieldClassifications => "fieldClassifications",
        ConfigurationSection.ReportFields => "reportFields",
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown section."),
    };

    private static void CheckValues(IReadOnlyList<ConfigurationValueEntry> values, List<FieldIssue> issues)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
        {
            ConfigurationValueEntry value = values[i];
            string path = $"values[{i}]";
            Code(value.Key, $"{path}.key", LongCodeLength, issues);
            Unique(keys, value.Key, $"{path}.key", issues);
            if (value.Value is null || value.Value.Length > ValueLength || !Parses(value.Type, value.Value))
            {
                issues.Add(new FieldIssue($"{path}.value", ContentIssueCodes.Malformed));
            }
            else if (value.Type == ConfigurationValueType.Percent && decimal.Parse(value.Value, CultureInfo.InvariantCulture) is < 0 or > 100)
            {
                issues.Add(new FieldIssue($"{path}.value", ContentIssueCodes.OutOfRange));
            }
        }
    }

    private static void CheckGovernanceProfiles(IReadOnlyList<GovernanceProfileEntry> profiles, ConfigurationReferences references, List<FieldIssue> issues)
    {
        HashSet<Guid> seen = [];
        for (int i = 0; i < profiles.Count; i++)
        {
            GovernanceProfileEntry profile = profiles[i];
            string path = $"governanceProfiles[{i}]";
            Item(profile.GovernanceProfileItemId, MasterDataCatalogueCodes.GovernanceProfile, $"{path}.governanceProfileItemId", references, issues);
            Unique(seen, profile.GovernanceProfileItemId, $"{path}.governanceProfileItemId", issues);
            Item(profile.DocumentControlLevelItemId, MasterDataCatalogueCodes.DocumentControlLevel, $"{path}.documentControlLevelItemId", references, issues);
            Range(profile.ChangeBandCount is >= 1 and <= 3, $"{path}.changeBandCount", issues);
            Range(profile.UpdateCadenceDays >= 1, $"{path}.updateCadenceDays", issues);
            Range(profile.AssignmentMinBudgetSar is null || IsAmountSar(profile.AssignmentMinBudgetSar.Value), $"{path}.assignmentMinBudgetSar", issues);
            Range(profile.AssignmentMinDurationDays is null or >= 1, $"{path}.assignmentMinDurationDays", issues);
            HashSet<string> fields = new(StringComparer.Ordinal);
            for (int f = 0; f < profile.MandatoryFieldCodes.Count; f++)
            {
                Code(profile.MandatoryFieldCodes[f], $"{path}.mandatoryFieldCodes[{f}]", LongCodeLength, issues);
                Unique(fields, profile.MandatoryFieldCodes[f], $"{path}.mandatoryFieldCodes[{f}]", issues);
            }
        }
    }

    private static void CheckMaterialityBands(IReadOnlyList<MaterialityBandEntry> bands, ConfigurationReferences references, List<FieldIssue> issues)
    {
        HashSet<(Guid, short)> seen = [];
        for (int i = 0; i < bands.Count; i++)
        {
            MaterialityBandEntry band = bands[i];
            string path = $"materialityBands[{i}]";
            Item(band.GovernanceProfileItemId, MasterDataCatalogueCodes.GovernanceProfile, $"{path}.governanceProfileItemId", references, issues);
            Range(Bands.Contains(band.BandNo), $"{path}.bandNo", issues);
            Unique(seen, (band.GovernanceProfileItemId, band.BandNo), $"{path}.bandNo", issues);
            Range(band.CostThresholdPct is null || IsMeasure(band.CostThresholdPct.Value), $"{path}.costThresholdPct", issues);
            Range(band.CostThresholdSar is null || IsAmountSar(band.CostThresholdSar.Value), $"{path}.costThresholdSar", issues);
            Range(band.ScheduleThresholdPct is null || IsMeasure(band.ScheduleThresholdPct.Value), $"{path}.scheduleThresholdPct", issues);
            Range(band.ScheduleThresholdDays is null or >= 0, $"{path}.scheduleThresholdDays", issues);
            if (band.ScopeRuleCode is not null)
            {
                Code(band.ScopeRuleCode, $"{path}.scopeRuleCode", LongCodeLength, issues);
            }
        }
    }

    private static void CheckRiskMatrix(ConfigurationContent content, ConfigurationReferences references, List<FieldIssue> issues)
    {
        HashSet<short> probabilities = [];
        for (int i = 0; i < content.ProbabilityLevels.Count; i++)
        {
            ProbabilityLevelEntry level = content.ProbabilityLevels[i];
            string path = $"probabilityLevels[{i}]";
            Range(Levels.Contains(level.Level), $"{path}.level", issues);
            Unique(probabilities, level.Level, $"{path}.level", issues);
            Range(Bounds(level.LowerPct, level.UpperPct) && level.LowerPct is null or (>= 0 and <= 100) && level.UpperPct is null or (>= 0 and <= 100), $"{path}.upperPct", issues);
        }

        HashSet<(Guid, short)> impacts = [];
        for (int i = 0; i < content.ImpactLevels.Count; i++)
        {
            ImpactLevelEntry level = content.ImpactLevels[i];
            string path = $"impactLevels[{i}]";
            Item(level.ImpactDimensionItemId, MasterDataCatalogueCodes.ImpactDimension, $"{path}.impactDimensionItemId", references, issues);
            Range(Levels.Contains(level.Level), $"{path}.level", issues);
            Unique(impacts, (level.ImpactDimensionItemId, level.Level), $"{path}.level", issues);
            Range(Bounds(level.LowerBound, level.UpperBound), $"{path}.upperBound", issues);
        }

        HashSet<string> ratings = new(StringComparer.Ordinal);
        for (int i = 0; i < content.RiskRatings.Count; i++)
        {
            Code(content.RiskRatings[i].Code, $"riskRatings[{i}].code", CodeLength, issues);
            Unique(ratings, content.RiskRatings[i].Code, $"riskRatings[{i}].code", issues);
        }

        HashSet<(short, short)> cells = [];
        for (int i = 0; i < content.RiskMatrixCells.Count; i++)
        {
            RiskMatrixCellEntry cell = content.RiskMatrixCells[i];
            string path = $"riskMatrixCells[{i}]";
            Range(Levels.Contains(cell.ProbabilityLevel), $"{path}.probabilityLevel", issues);
            Range(Levels.Contains(cell.ImpactLevel), $"{path}.impactLevel", issues);
            Unique(cells, (cell.ProbabilityLevel, cell.ImpactLevel), $"{path}.impactLevel", issues);
            if (!ratings.Contains(cell.RatingCode ?? string.Empty))
            {
                issues.Add(new FieldIssue($"{path}.ratingCode", FieldIssue.NotFound));
            }
        }
    }

    private static void CheckApprovalAuthority(IReadOnlyList<ApprovalAuthorityEntry> rules, ConfigurationReferences references, List<FieldIssue> issues)
    {
        for (int i = 0; i < rules.Count; i++)
        {
            ApprovalAuthorityEntry rule = rules[i];
            string path = $"approvalAuthority[{i}]";
            Code(rule.SubjectTypeCode, $"{path}.subjectTypeCode", LongCodeLength, issues);
            if (rule.GovernanceProfileItemId is { } profile)
            {
                Item(profile, MasterDataCatalogueCodes.GovernanceProfile, $"{path}.governanceProfileItemId", references, issues);
            }

            Range(rule.BandNo is null || Bands.Contains(rule.BandNo.Value), $"{path}.bandNo", issues);
            Range(rule.MinAmountSar is null || IsAmountSar(rule.MinAmountSar.Value), $"{path}.minAmountSar", issues);
            Range(rule.SequenceNo >= 1, $"{path}.sequenceNo", issues);
            Role(rule.ApproverRoleId, $"{path}.approverRoleId", references, issues);
        }
    }

    private static void CheckNotificationRouting(IReadOnlyList<NotificationEventFamilyEntry> families, ConfigurationReferences references, List<FieldIssue> issues)
    {
        HashSet<string> codes = new(StringComparer.Ordinal);
        for (int i = 0; i < families.Count; i++)
        {
            NotificationEventFamilyEntry family = families[i];
            string path = $"notificationEventFamilies[{i}]";
            Code(family.Code, $"{path}.code", LongCodeLength, issues);
            Unique(codes, family.Code, $"{path}.code", issues);
            HashSet<NotificationChannel> channels = [];
            for (int c = 0; c < family.Channels.Count; c++)
            {
                Unique(channels, family.Channels[c].Channel, $"{path}.channels[{c}].channel", issues);
            }

            HashSet<Guid> roles = [];
            for (int r = 0; r < family.RecipientRoleIds.Count; r++)
            {
                Role(family.RecipientRoleIds[r], $"{path}.recipientRoleIds[{r}]", references, issues);
                Unique(roles, family.RecipientRoleIds[r], $"{path}.recipientRoleIds[{r}]", issues);
            }
        }
    }

    private static void CheckKpiPolicies(IReadOnlyList<KpiPolicyEntry> policies, ConfigurationReferences references, List<FieldIssue> issues)
    {
        HashSet<Guid> seen = [];
        for (int i = 0; i < policies.Count; i++)
        {
            KpiPolicyEntry policy = policies[i];
            string path = $"kpiPolicies[{i}]";
            if (!references.KpiDefinitions.TryGetValue(policy.KpiDefinitionId, out GovernedLifecycleState state))
            {
                issues.Add(new FieldIssue($"{path}.kpiDefinitionId", FieldIssue.NotFound));
            }
            else if (state != GovernedLifecycleState.Published)
            {
                issues.Add(new FieldIssue($"{path}.kpiDefinitionId", ContentIssueCodes.NotPublished));
            }

            Unique(seen, policy.KpiDefinitionId, $"{path}.kpiDefinitionId", issues);
            Range(policy.GreenThreshold is null || IsSignedMeasure(policy.GreenThreshold.Value), $"{path}.greenThreshold", issues);
            Range(policy.AmberThreshold is null || IsSignedMeasure(policy.AmberThreshold.Value), $"{path}.amberThreshold", issues);
        }
    }

    private static void CheckParticipation(IReadOnlyList<ParticipationRuleEntry> rules, ConfigurationReferences references, List<FieldIssue> issues)
    {
        HashSet<(ParticipationMode, Guid)> seen = [];
        for (int i = 0; i < rules.Count; i++)
        {
            ParticipationRuleEntry rule = rules[i];
            string path = $"participationRules[{i}]";
            Item(rule.ContributionTypeItemId, MasterDataCatalogueCodes.ContributionType, $"{path}.contributionTypeItemId", references, issues);
            Unique(seen, (rule.ParticipationMode, rule.ContributionTypeItemId), $"{path}.contributionTypeItemId", issues);
        }
    }

    private static void CheckEvidence(IReadOnlyList<EvidenceRequirementEntry> rules, ConfigurationReferences references, List<FieldIssue> issues)
    {
        HashSet<(Guid, Guid)> seen = [];
        for (int i = 0; i < rules.Count; i++)
        {
            EvidenceRequirementEntry rule = rules[i];
            string path = $"evidenceRequirements[{i}]";
            Item(rule.MilestoneCategoryItemId, MasterDataCatalogueCodes.MilestoneCategory, $"{path}.milestoneCategoryItemId", references, issues);
            Item(rule.EvidenceTypeItemId, MasterDataCatalogueCodes.EvidenceType, $"{path}.evidenceTypeItemId", references, issues);
            Unique(seen, (rule.MilestoneCategoryItemId, rule.EvidenceTypeItemId), $"{path}.evidenceTypeItemId", issues);
        }
    }

    private static void CheckFieldClassifications(IReadOnlyList<FieldClassificationEntry> rules, ConfigurationReferences references, List<FieldIssue> issues)
    {
        HashSet<(string, string)> seen = [];
        for (int i = 0; i < rules.Count; i++)
        {
            FieldClassificationEntry rule = rules[i];
            string path = $"fieldClassifications[{i}]";
            Code(rule.EntityCode, $"{path}.entityCode", LongCodeLength, issues);
            Code(rule.FieldCode, $"{path}.fieldCode", LongCodeLength, issues);
            Unique(seen, (rule.EntityCode, rule.FieldCode), $"{path}.fieldCode", issues);
            Item(rule.DataClassificationItemId, MasterDataCatalogueCodes.DataClassification, $"{path}.dataClassificationItemId", references, issues);
        }
    }

    private static void CheckReportFields(IReadOnlyList<ReportFieldEntry> fields, ConfigurationReferences references, List<FieldIssue> issues)
    {
        HashSet<(string, string)> seen = [];
        for (int i = 0; i < fields.Count; i++)
        {
            ReportFieldEntry field = fields[i];
            string path = $"reportFields[{i}]";
            Code(field.SourceEntityCode, $"{path}.sourceEntityCode", LongCodeLength, issues);
            Code(field.FieldCode, $"{path}.fieldCode", LongCodeLength, issues);
            Unique(seen, (field.SourceEntityCode, field.FieldCode), $"{path}.fieldCode", issues);
            if (field.DataClassificationItemId is { } classification)
            {
                Item(classification, MasterDataCatalogueCodes.DataClassification, $"{path}.dataClassificationItemId", references, issues);
            }
        }
    }

    private static void RequireLevels(string path, IEnumerable<short> levels, List<FieldIssue> issues)
    {
        if (!levels.Order().SequenceEqual(Levels))
        {
            issues.Add(new FieldIssue(path, ContentIssueCodes.Incomplete));
        }
    }

    private static void Item(Guid itemId, string catalogueCode, string path, ConfigurationReferences references, List<FieldIssue> issues)
    {
        if (!references.Items.TryGetValue(itemId, out ItemFacts? item))
        {
            issues.Add(new FieldIssue(path, FieldIssue.NotFound));
        }
        else if (item.CatalogueCode != catalogueCode)
        {
            issues.Add(new FieldIssue(path, ContentIssueCodes.WrongCatalogue));
        }
        else if (item.LifecycleState != GovernedLifecycleState.Published)
        {
            issues.Add(new FieldIssue(path, ContentIssueCodes.NotPublished));
        }
    }

    private static void Role(Guid roleId, string path, ConfigurationReferences references, List<FieldIssue> issues)
    {
        if (!references.RoleIds.Contains(roleId))
        {
            issues.Add(new FieldIssue(path, FieldIssue.NotFound));
        }
    }

    private static void Code(string? value, string path, int maxLength, List<FieldIssue> issues)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maxLength || !CodeShape().IsMatch(value))
        {
            issues.Add(new FieldIssue(path, ContentIssueCodes.Malformed));
        }
    }

    private static void Unique<T>(HashSet<T> seen, T key, string path, List<FieldIssue> issues)
    {
        if (!seen.Add(key))
        {
            issues.Add(new FieldIssue(path, FieldIssue.Duplicate));
        }
    }

    private static void Range(bool inRange, string path, List<FieldIssue> issues)
    {
        if (!inRange)
        {
            issues.Add(new FieldIssue(path, ContentIssueCodes.OutOfRange));
        }
    }

    private static bool Bounds(decimal? lower, decimal? upper) =>
        (lower is null || IsSignedMeasure(lower.Value)) && (upper is null || IsSignedMeasure(upper.Value)) && !(lower > upper);

    private static bool IsAmountSar(decimal amount) => amount >= 0 && amount <= MaxAmountSar && decimal.Round(amount, 2) == amount;

    private static bool IsMeasure(decimal value) => value >= 0 && IsSignedMeasure(value);

    private static bool IsSignedMeasure(decimal value) => Math.Abs(value) <= MaxMeasure && decimal.Round(value, 4) == value;

    private static bool Parses(ConfigurationValueType type, string text) => type switch
    {
        ConfigurationValueType.Integer => long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
        ConfigurationValueType.DurationDays => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _),
        ConfigurationValueType.Decimal or ConfigurationValueType.Percent =>
            decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _),
        ConfigurationValueType.Boolean => text is "true" or "false",
        ConfigurationValueType.Text => text.Length > 0,
        _ => false,
    };

    [GeneratedRegex("^[A-Z0-9][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeShape();
}
