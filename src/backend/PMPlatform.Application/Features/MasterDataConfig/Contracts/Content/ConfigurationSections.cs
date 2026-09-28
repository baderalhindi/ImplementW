namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>
/// Which sections each family carries (ERD §5.3). Scalar <see cref="ConfigurationSection.Values"/> are open to every
/// family; DASHBOARD_RULES and WORKFLOW_POLICY carry nothing else.
/// </summary>
public static class ConfigurationSections
{
    private static readonly Dictionary<string, ConfigurationSection[]> TypedSections = new(StringComparer.Ordinal)
    {
        [ConfigurationFamilyCodes.GovernanceProfile] = [ConfigurationSection.GovernanceProfiles],
        [ConfigurationFamilyCodes.MaterialityBand] = [ConfigurationSection.MaterialityBands],
        [ConfigurationFamilyCodes.RiskMatrix] =
            [ConfigurationSection.ProbabilityLevels, ConfigurationSection.ImpactLevels, ConfigurationSection.RiskRatings, ConfigurationSection.RiskMatrixCells],
        [ConfigurationFamilyCodes.ApprovalAuthority] = [ConfigurationSection.ApprovalAuthority],
        [ConfigurationFamilyCodes.NotificationRouting] = [ConfigurationSection.NotificationEventFamilies],
        [ConfigurationFamilyCodes.KpiPolicy] = [ConfigurationSection.KpiPolicies],
        [ConfigurationFamilyCodes.Participation] = [ConfigurationSection.ParticipationRules],
        [ConfigurationFamilyCodes.EvidencePolicy] = [ConfigurationSection.EvidenceRequirements],
        [ConfigurationFamilyCodes.FieldClassification] = [ConfigurationSection.FieldClassifications],
        [ConfigurationFamilyCodes.ReportRules] = [ConfigurationSection.ReportFields],
        [ConfigurationFamilyCodes.DashboardRules] = [],
        [ConfigurationFamilyCodes.WorkflowPolicy] = [],
    };

    public static IReadOnlyCollection<string> FamilyCodes => TypedSections.Keys;

    /// <summary>The sections <paramref name="familyCode"/> carries. A family this build does not know carries nothing (fail closed).</summary>
    public static IReadOnlySet<ConfigurationSection> Of(string familyCode) =>
        TypedSections.TryGetValue(familyCode, out ConfigurationSection[]? sections)
            ? new HashSet<ConfigurationSection>([ConfigurationSection.Values, .. sections])
            : [];

    /// <summary>How many rows each section of <paramref name="content"/> holds.</summary>
    public static IReadOnlyDictionary<ConfigurationSection, int> Counts(ConfigurationContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new Dictionary<ConfigurationSection, int>
        {
            [ConfigurationSection.Values] = content.Values.Count,
            [ConfigurationSection.GovernanceProfiles] = content.GovernanceProfiles.Count,
            [ConfigurationSection.MaterialityBands] = content.MaterialityBands.Count,
            [ConfigurationSection.ProbabilityLevels] = content.ProbabilityLevels.Count,
            [ConfigurationSection.ImpactLevels] = content.ImpactLevels.Count,
            [ConfigurationSection.RiskRatings] = content.RiskRatings.Count,
            [ConfigurationSection.RiskMatrixCells] = content.RiskMatrixCells.Count,
            [ConfigurationSection.ApprovalAuthority] = content.ApprovalAuthority.Count,
            [ConfigurationSection.NotificationEventFamilies] = content.NotificationEventFamilies.Count,
            [ConfigurationSection.KpiPolicies] = content.KpiPolicies.Count,
            [ConfigurationSection.ParticipationRules] = content.ParticipationRules.Count,
            [ConfigurationSection.EvidenceRequirements] = content.EvidenceRequirements.Count,
            [ConfigurationSection.FieldClassifications] = content.FieldClassifications.Count,
            [ConfigurationSection.ReportFields] = content.ReportFields.Count,
        };
    }
}
