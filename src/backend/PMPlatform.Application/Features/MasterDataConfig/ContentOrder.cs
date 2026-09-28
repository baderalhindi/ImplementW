using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>
/// One order for every section, by its natural key, so the same content always reads and fingerprints the same way
/// whatever order it was written or stored in.
/// </summary>
internal static class ContentOrder
{
    public static ConfigurationContent Canonical(ConfigurationContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new ConfigurationContent
        {
            Values = [.. content.Values.OrderBy(v => v.Key, StringComparer.Ordinal)],
            GovernanceProfiles =
            [
                .. content.GovernanceProfiles
                    .OrderBy(p => p.GovernanceProfileItemId)
                    .Select(p => p with { MandatoryFieldCodes = [.. p.MandatoryFieldCodes.Order(StringComparer.Ordinal)] }),
            ],
            MaterialityBands = [.. content.MaterialityBands.OrderBy(b => b.GovernanceProfileItemId).ThenBy(b => b.BandNo)],
            ProbabilityLevels = [.. content.ProbabilityLevels.OrderBy(l => l.Level)],
            ImpactLevels = [.. content.ImpactLevels.OrderBy(l => l.ImpactDimensionItemId).ThenBy(l => l.Level)],
            RiskRatings = [.. content.RiskRatings.OrderBy(r => r.SortOrder).ThenBy(r => r.Code, StringComparer.Ordinal)],
            RiskMatrixCells = [.. content.RiskMatrixCells.OrderBy(c => c.ProbabilityLevel).ThenBy(c => c.ImpactLevel)],
            ApprovalAuthority =
            [
                .. content.ApprovalAuthority
                    .OrderBy(r => r.SubjectTypeCode, StringComparer.Ordinal).ThenBy(r => r.GovernanceProfileItemId).ThenBy(r => r.BandNo)
                    .ThenBy(r => r.MinAmountSar).ThenBy(r => r.SequenceNo).ThenBy(r => r.ApproverRoleId),
            ],
            NotificationEventFamilies =
            [
                .. content.NotificationEventFamilies
                    .OrderBy(f => f.Code, StringComparer.Ordinal)
                    .Select(f => f with { Channels = [.. f.Channels.OrderBy(c => c.Channel)], RecipientRoleIds = [.. f.RecipientRoleIds.Order()] }),
            ],
            KpiPolicies = [.. content.KpiPolicies.OrderBy(p => p.KpiDefinitionId)],
            ParticipationRules = [.. content.ParticipationRules.OrderBy(r => r.ParticipationMode).ThenBy(r => r.ContributionTypeItemId)],
            EvidenceRequirements = [.. content.EvidenceRequirements.OrderBy(r => r.MilestoneCategoryItemId).ThenBy(r => r.EvidenceTypeItemId)],
            FieldClassifications =
                [.. content.FieldClassifications.OrderBy(r => r.EntityCode, StringComparer.Ordinal).ThenBy(r => r.FieldCode, StringComparer.Ordinal)],
            ReportFields = [.. content.ReportFields.OrderBy(f => f.SourceEntityCode, StringComparer.Ordinal).ThenBy(f => f.FieldCode, StringComparer.Ordinal)],
        };
    }
}
