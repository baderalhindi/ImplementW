using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Api.Models.MasterDataConfig;

/// <summary>
/// The whole content of a DRAFT configuration version (T-3: shape here, meaning in the module). An absent section is
/// empty. Every required property of every entry is checked for presence and named by its path, e.g.
/// <c>riskMatrixCells[3].ratingCode</c>; ranges, codes and references are the module's rules (422).
/// </summary>
public sealed record ConfigurationContentRequest(
    IReadOnlyList<ConfigurationValueRequest?>? Values,
    IReadOnlyList<GovernanceProfileRequest?>? GovernanceProfiles,
    IReadOnlyList<MaterialityBandRequest?>? MaterialityBands,
    IReadOnlyList<ProbabilityLevelRequest?>? ProbabilityLevels,
    IReadOnlyList<ImpactLevelRequest?>? ImpactLevels,
    IReadOnlyList<RiskRatingRequest?>? RiskRatings,
    IReadOnlyList<RiskMatrixCellRequest?>? RiskMatrixCells,
    IReadOnlyList<ApprovalAuthorityRequest?>? ApprovalAuthority,
    IReadOnlyList<NotificationEventFamilyRequest?>? NotificationEventFamilies,
    IReadOnlyList<KpiPolicyRequest?>? KpiPolicies,
    IReadOnlyList<ParticipationRuleRequest?>? ParticipationRules,
    IReadOnlyList<EvidenceRequirementRequest?>? EvidenceRequirements,
    IReadOnlyList<FieldClassificationRequest?>? FieldClassifications,
    IReadOnlyList<ReportFieldRequest?>? ReportFields)
{
    internal List<FieldError> Validate(out ConfigurationContent? content)
    {
        EntryReader reader = new();
        content = new ConfigurationContent
        {
            Values = reader.Section(Values, "values", (v, p) => new ConfigurationValueEntry(
                reader.Text(v.Key, $"{p}.key"), reader.Required(v.Type, $"{p}.type"), reader.Text(v.Value, $"{p}.value"))),
            GovernanceProfiles = reader.Section(GovernanceProfiles, "governanceProfiles", (g, p) => new GovernanceProfileEntry(
                reader.Id(g.GovernanceProfileItemId, $"{p}.governanceProfileItemId"),
                reader.Required(g.RequiresBaselineApproval, $"{p}.requiresBaselineApproval"),
                reader.Required(g.RiskManagementRequired, $"{p}.riskManagementRequired"),
                reader.Required(g.ChangeBandCount, $"{p}.changeBandCount"),
                reader.Required(g.UpdateCadenceDays, $"{p}.updateCadenceDays"),
                reader.Id(g.DocumentControlLevelItemId, $"{p}.documentControlLevelItemId"),
                reader.Required(g.IncludedInReporting, $"{p}.includedInReporting"),
                g.AssignmentMinBudgetSar,
                g.AssignmentMinDurationDays,
                [.. (g.MandatoryFieldCodes ?? []).Select((code, i) => reader.Text(code, $"{p}.mandatoryFieldCodes[{i}]"))])),
            MaterialityBands = reader.Section(MaterialityBands, "materialityBands", (b, p) => new MaterialityBandEntry(
                reader.Id(b.GovernanceProfileItemId, $"{p}.governanceProfileItemId"),
                reader.Required(b.BandNo, $"{p}.bandNo"),
                b.CostThresholdPct,
                b.CostThresholdSar,
                b.ScheduleThresholdPct,
                b.ScheduleThresholdDays,
                b.ScopeRuleCode,
                reader.Required(b.RequiresApproval, $"{p}.requiresApproval"))),
            ProbabilityLevels = reader.Section(ProbabilityLevels, "probabilityLevels", (l, p) => new ProbabilityLevelEntry(
                reader.Required(l.Level, $"{p}.level"), reader.Label(l.Label, $"{p}.label"), l.LowerPct, l.UpperPct)),
            ImpactLevels = reader.Section(ImpactLevels, "impactLevels", (l, p) => new ImpactLevelEntry(
                reader.Id(l.ImpactDimensionItemId, $"{p}.impactDimensionItemId"),
                reader.Required(l.Level, $"{p}.level"),
                reader.Label(l.Label, $"{p}.label"),
                reader.OptionalLabel(l.Description, $"{p}.description"),
                l.LowerBound,
                l.UpperBound)),
            RiskRatings = reader.Section(RiskRatings, "riskRatings", (r, p) => new RiskRatingEntry(
                reader.Text(r.Code, $"{p}.code"), reader.Label(r.Label, $"{p}.label"), r.SortOrder ?? 0)),
            RiskMatrixCells = reader.Section(RiskMatrixCells, "riskMatrixCells", (c, p) => new RiskMatrixCellEntry(
                reader.Required(c.ProbabilityLevel, $"{p}.probabilityLevel"), reader.Required(c.ImpactLevel, $"{p}.impactLevel"), reader.Text(c.RatingCode, $"{p}.ratingCode"))),
            ApprovalAuthority = reader.Section(ApprovalAuthority, "approvalAuthority", (a, p) => new ApprovalAuthorityEntry(
                reader.Text(a.SubjectTypeCode, $"{p}.subjectTypeCode"),
                a.GovernanceProfileItemId,
                a.BandNo,
                a.MinAmountSar,
                reader.Required(a.SequenceNo, $"{p}.sequenceNo"),
                reader.Id(a.ApproverRoleId, $"{p}.approverRoleId"),
                a.IsMandatory ?? true)),
            NotificationEventFamilies = reader.Section(NotificationEventFamilies, "notificationEventFamilies", (f, p) => new NotificationEventFamilyEntry(
                reader.Text(f.Code, $"{p}.code"),
                reader.Label(f.Label, $"{p}.label"),
                reader.Required(f.IsMandatory, $"{p}.isMandatory"),
                reader.Section(f.Channels, $"{p}.channels", (c, cp) => new NotificationChannelEntry(
                    reader.Required(c.Channel, $"{cp}.channel"), reader.Required(c.EnabledByDefault, $"{cp}.enabledByDefault"))),
                [.. (f.RecipientRoleIds ?? []).Select((id, i) => reader.Id(id, $"{p}.recipientRoleIds[{i}]"))])),
            KpiPolicies = reader.Section(KpiPolicies, "kpiPolicies", (k, p) => new KpiPolicyEntry(
                reader.Id(k.KpiDefinitionId, $"{p}.kpiDefinitionId"), k.CalculationExpression, k.GreenThreshold, k.AmberThreshold)),
            ParticipationRules = reader.Section(ParticipationRules, "participationRules", (r, p) => new ParticipationRuleEntry(
                reader.Required(r.ParticipationMode, $"{p}.participationMode"),
                reader.Id(r.ContributionTypeItemId, $"{p}.contributionTypeItemId"),
                reader.Required(r.IsEnabled, $"{p}.isEnabled"))),
            EvidenceRequirements = reader.Section(EvidenceRequirements, "evidenceRequirements", (r, p) => new EvidenceRequirementEntry(
                reader.Id(r.MilestoneCategoryItemId, $"{p}.milestoneCategoryItemId"),
                reader.Id(r.EvidenceTypeItemId, $"{p}.evidenceTypeItemId"),
                reader.Required(r.IsMandatory, $"{p}.isMandatory"))),
            FieldClassifications = reader.Section(FieldClassifications, "fieldClassifications", (r, p) => new FieldClassificationEntry(
                reader.Text(r.EntityCode, $"{p}.entityCode"),
                reader.Text(r.FieldCode, $"{p}.fieldCode"),
                reader.Id(r.DataClassificationItemId, $"{p}.dataClassificationItemId"),
                reader.Required(r.MaskingRule, $"{p}.maskingRule"))),
            ReportFields = reader.Section(ReportFields, "reportFields", (f, p) => new ReportFieldEntry(
                reader.Text(f.SourceEntityCode, $"{p}.sourceEntityCode"),
                reader.Text(f.FieldCode, $"{p}.fieldCode"),
                reader.Label(f.Label, $"{p}.label"),
                f.IsFilterable ?? false,
                f.IsSortable ?? false,
                f.DataClassificationItemId)),
        };

        if (reader.Errors.Count > 0)
        {
            content = null;
        }

        return reader.Errors;
    }

    /// <summary>Reads entries, recording each absent required value; a placeholder stands in so reading can go on.</summary>
    private sealed class EntryReader
    {
        public List<FieldError> Errors { get; } = [];

        public List<TEntry> Section<TRequest, TEntry>(IReadOnlyList<TRequest?>? requests, string path, Func<TRequest, string, TEntry> read)
            where TRequest : class
        {
            List<TEntry> entries = [];
            for (int i = 0; i < (requests?.Count ?? 0); i++)
            {
                if (requests![i] is { } request)
                {
                    entries.Add(read(request, $"{path}[{i}]"));
                }
                else
                {
                    Errors.Add(new FieldError($"{path}[{i}]", FieldError.Required));
                }
            }

            return entries;
        }

        public T Required<T>(T? value, string path)
            where T : struct
        {
            if (value is null)
            {
                Errors.Add(new FieldError(path, FieldError.Required));
            }

            return value ?? default;
        }

        public Guid Id(Guid? value, string path)
        {
            RequestValidation.RequireId(value, path, Errors);
            return value ?? Guid.Empty;
        }

        public string Text(string? value, string path)
        {
            if (string.IsNullOrEmpty(value))
            {
                Errors.Add(new FieldError(path, FieldError.Required));
            }

            return value ?? string.Empty;
        }

        public BilingualLabel Label(BilingualLabelRequest? value, string path)
        {
            int before = Errors.Count;
            RequestValidation.Label(value, path, Errors);
            return Errors.Count == before ? value!.ToLabel() : Placeholder;
        }

        public BilingualLabel? OptionalLabel(BilingualLabelRequest? value, string path) => MasterDataConfig.OptionalLabel.Validate(value, path, Errors);

        private static BilingualLabel Placeholder { get; } = new("-", "-");
    }
}

public sealed record ConfigurationValueRequest(string? Key, ConfigurationValueType? Type, string? Value);

public sealed record GovernanceProfileRequest(
    Guid? GovernanceProfileItemId,
    bool? RequiresBaselineApproval,
    bool? RiskManagementRequired,
    short? ChangeBandCount,
    int? UpdateCadenceDays,
    Guid? DocumentControlLevelItemId,
    bool? IncludedInReporting,
    decimal? AssignmentMinBudgetSar,
    int? AssignmentMinDurationDays,
    IReadOnlyList<string?>? MandatoryFieldCodes);

public sealed record MaterialityBandRequest(
    Guid? GovernanceProfileItemId,
    short? BandNo,
    decimal? CostThresholdPct,
    decimal? CostThresholdSar,
    decimal? ScheduleThresholdPct,
    int? ScheduleThresholdDays,
    string? ScopeRuleCode,
    bool? RequiresApproval);

public sealed record ProbabilityLevelRequest(short? Level, BilingualLabelRequest? Label, decimal? LowerPct, decimal? UpperPct);

public sealed record ImpactLevelRequest(
    Guid? ImpactDimensionItemId, short? Level, BilingualLabelRequest? Label, BilingualLabelRequest? Description, decimal? LowerBound, decimal? UpperBound);

public sealed record RiskRatingRequest(string? Code, BilingualLabelRequest? Label, short? SortOrder);

public sealed record RiskMatrixCellRequest(short? ProbabilityLevel, short? ImpactLevel, string? RatingCode);

public sealed record ApprovalAuthorityRequest(
    string? SubjectTypeCode, Guid? GovernanceProfileItemId, short? BandNo, decimal? MinAmountSar, short? SequenceNo, Guid? ApproverRoleId, bool? IsMandatory);

public sealed record NotificationEventFamilyRequest(
    string? Code, BilingualLabelRequest? Label, bool? IsMandatory, IReadOnlyList<NotificationChannelRequest?>? Channels, IReadOnlyList<Guid?>? RecipientRoleIds);

public sealed record NotificationChannelRequest(NotificationChannel? Channel, bool? EnabledByDefault);

public sealed record KpiPolicyRequest(Guid? KpiDefinitionId, string? CalculationExpression, decimal? GreenThreshold, decimal? AmberThreshold);

public sealed record ParticipationRuleRequest(ParticipationMode? ParticipationMode, Guid? ContributionTypeItemId, bool? IsEnabled);

public sealed record EvidenceRequirementRequest(Guid? MilestoneCategoryItemId, Guid? EvidenceTypeItemId, bool? IsMandatory);

public sealed record FieldClassificationRequest(string? EntityCode, string? FieldCode, Guid? DataClassificationItemId, MaskingRule? MaskingRule);

public sealed record ReportFieldRequest(
    string? SourceEntityCode, string? FieldCode, BilingualLabelRequest? Label, bool? IsFilterable, bool? IsSortable, Guid? DataClassificationItemId);
