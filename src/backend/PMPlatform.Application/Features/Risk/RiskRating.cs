using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.Risk.Contracts;

namespace PMPlatform.Application.Features.Risk;

/// <summary>
/// How an assessment is rated against one RISK_MATRIX version (ADR-011, PTBC-017), generically, since the level boundaries,
/// the 5×5 mapping and the labels are outstanding (OQ-006): the version defines the probability levels, the dimensions and
/// their levels, and the cells; nothing here names a value. The overall impact is the highest level of any dimension
/// (risk-management.md D-5), and the rating is the version's cell for the probability and the overall impact.
/// </summary>
internal static class RiskRating
{
    /// <summary>
    /// Every way <paramref name="draft"/> does not fit <paramref name="matrix"/>: a probability level it does not define, a
    /// dimension it defines missing or given twice, a dimension it does not define, a level it does not define for the dimension.
    /// Empty when it fits.
    /// </summary>
    public static IReadOnlyList<FieldIssue> Check(ConfigurationContent matrix, RiskAssessmentDraft draft)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(draft);

        List<FieldIssue> issues = [];
        if (!matrix.ProbabilityLevels.Any(p => p.Level == draft.ProbabilityLevel))
        {
            issues.Add(new FieldIssue("probabilityLevel", FieldIssue.NotAllowed));
        }

        ILookup<Guid, short> levels = matrix.ImpactLevels.ToLookup(l => l.ImpactDimensionItemId, l => l.Level);
        HashSet<Guid> seen = [];
        for (int i = 0; i < draft.Impacts.Count; i++)
        {
            RiskImpactInput impact = draft.Impacts[i];
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

        if (levels.Any(dimension => !seen.Contains(dimension.Key)))
        {
            issues.Add(new FieldIssue("impacts", FieldIssue.Required));
        }

        return issues;
    }

    /// <summary>The overall impact: the highest level of any dimension, so no dimension's severity is averaged away.</summary>
    public static short OverallImpactOf(IEnumerable<RiskImpactInput> impacts) => impacts.Max(i => i.ImpactLevel);
}
