using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.MasterDataConfig;

/// <summary>
/// Configuration families, versions and the typed rows of ERD §5.3 (TASK-034). A version's content is read and written
/// as one <see cref="ConfigurationContent"/>; the <c>guard_configuration_content</c> trigger refuses any write to the
/// rows of a version that is not DRAFT.
/// </summary>
internal sealed class ConfigurationRepository(PMPlatformDbContext context) : IConfigurationRepository
{
    public async Task<IReadOnlyList<ConfigurationFamily>> ListFamiliesAsync(CancellationToken cancellationToken) =>
        await context.Set<ConfigurationFamily>().AsNoTracking().OrderBy(f => f.Code).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<ConfigurationFamily?> FindFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
        context.Set<ConfigurationFamily>().AsNoTracking().SingleOrDefaultAsync(f => f.Id == familyId, cancellationToken);

    public Task<ConfigurationFamily?> FindFamilyByCodeAsync(string code, CancellationToken cancellationToken) =>
        context.Set<ConfigurationFamily>().AsNoTracking().SingleOrDefaultAsync(f => f.Code == code, cancellationToken);

    public async Task<IReadOnlyList<(Guid FamilyId, PublishedVersionWindow Window)>> ListPublishedWindowsAsync(
        IReadOnlyCollection<Guid> familyIds, CancellationToken cancellationToken)
    {
        List<Guid> ids = [.. familyIds];
        var rows = await context.Set<ConfigurationVersion>().AsNoTracking()
            .Where(v => ids.Contains(v.ConfigurationFamilyId) && v.PublishedAt != null)
            .Select(v => new { v.ConfigurationFamilyId, v.Id, v.VersionNo, v.EffectiveFrom, v.EffectiveTo })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. rows.Select(r => (r.ConfigurationFamilyId, new PublishedVersionWindow(r.Id, r.VersionNo, r.EffectiveFrom!.Value, r.EffectiveTo)))];
    }

    public async Task<(IReadOnlyList<ConfigurationVersionRow> Rows, int TotalCount)> ListVersionsAsync(
        ConfigurationVersionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<ConfigurationVersion> versions = context.Set<ConfigurationVersion>().AsNoTracking();
        if (query.FamilyId is { } familyId)
        {
            versions = versions.Where(v => v.ConfigurationFamilyId == familyId);
        }

        if (query.LifecycleStates.Count > 0)
        {
            List<GovernedLifecycleState> states = [.. query.LifecycleStates];
            versions = versions.Where(v => states.Contains(v.LifecycleState));
        }

        int totalCount = await versions.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await versions
            .Join(context.Set<ConfigurationFamily>(), v => v.ConfigurationFamilyId, f => f.Id, (v, f) => new { Version = v, FamilyCode = f.Code })
            .OrderBy(x => x.FamilyCode).ThenByDescending(x => x.Version.VersionNo)
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return ([.. rows.Select(r => new ConfigurationVersionRow(r.Version, r.FamilyCode))], totalCount);
    }

    public async Task<Versioned<ConfigurationVersionRow>?> FindVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var row = await context.Set<ConfigurationVersion>().AsNoTracking()
            .Where(v => v.Id == versionId)
            .Join(context.Set<ConfigurationFamily>(), v => v.ConfigurationFamilyId, f => f.Id, (v, f) => new
            {
                Version = v,
                FamilyCode = f.Code,
                RowVersion = EF.Property<uint>(v, EntityTypeBuilderExtensions.RowVersion),
            })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? null : new Versioned<ConfigurationVersionRow>(new ConfigurationVersionRow(row.Version, row.FamilyCode), row.RowVersion);
    }

    public async Task<ConfigurationVersion?> FindVersionForUpdateAsync(Guid versionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ConfigurationVersion? version = await context.Set<ConfigurationVersion>().SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken).ConfigureAwait(false);
        if (version is not null)
        {
            AdministrationPersistence.ExpectVersion(context, version, expectedVersion);
        }

        return version;
    }

    public async Task<int> GetLatestVersionNoAsync(Guid familyId, CancellationToken cancellationToken) =>
        await context.Set<ConfigurationVersion>().AsNoTracking()
            .Where(v => v.ConfigurationFamilyId == familyId)
            .MaxAsync(v => (int?)v.VersionNo, cancellationToken).ConfigureAwait(false) ?? 0;

    public async Task<IReadOnlyList<RiskRatingDefinition>> ListRiskRatingsAsync(Guid versionId, CancellationToken cancellationToken) =>
        await Rows<RiskRatingDefinition>(versionId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportAllowlistEntry>> ListReportAllowlistAsync(
        IReadOnlyCollection<Guid>? versionIds, IReadOnlyCollection<Guid>? entryIds, CancellationToken cancellationToken) =>
        await context.Set<ReportAllowlistEntry>().AsNoTracking()
            .Where(e => (versionIds == null || versionIds.Contains(e.ConfigurationVersionId)) && (entryIds == null || entryIds.Contains(e.Id)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<ConfigurationContent> ReadContentAsync(Guid versionId, CancellationToken cancellationToken)
    {
        List<GovernanceProfileSetting> settings = await Rows<GovernanceProfileSetting>(versionId).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<Guid> settingIds = [.. settings.Select(s => s.Id)];
        ILookup<Guid, string> mandatoryFields = (await context.Set<GovernanceProfileMandatoryField>().AsNoTracking()
                .Where(f => settingIds.Contains(f.GovernanceProfileSettingId))
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(f => f.GovernanceProfileSettingId, f => f.FieldCode);

        List<NotificationEventFamily> eventFamilies = await Rows<NotificationEventFamily>(versionId).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<Guid> eventFamilyIds = [.. eventFamilies.Select(f => f.Id)];
        ILookup<Guid, NotificationChannelRule> channels = (await context.Set<NotificationChannelRule>().AsNoTracking()
                .Where(c => eventFamilyIds.Contains(c.NotificationEventFamilyId))
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(c => c.NotificationEventFamilyId);
        ILookup<Guid, Guid> recipients = (await context.Set<NotificationRecipientRule>().AsNoTracking()
                .Where(r => eventFamilyIds.Contains(r.NotificationEventFamilyId))
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(r => r.NotificationEventFamilyId, r => r.RoleId);

        List<RiskRatingDefinition> ratings = await Rows<RiskRatingDefinition>(versionId).ToListAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, string> ratingCodes = ratings.ToDictionary(r => r.Id, r => r.Code);

        return new ConfigurationContent
        {
            Values = await Rows<ConfigurationValue>(versionId)
                .Select(v => new ConfigurationValueEntry(v.ValueKey, v.ValueType, v.ValueText))
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            GovernanceProfiles =
            [
                .. settings.Select(s => new GovernanceProfileEntry(
                    s.GovernanceProfileItemId, s.RequiresBaselineApproval, s.RiskManagementRequired, s.ChangeBandCount, s.UpdateCadenceDays,
                    s.DocumentControlLevelItemId, s.IncludedInReporting, s.AssignmentMinBudgetSar?.Amount, s.AssignmentMinDurationDays,
                    [.. mandatoryFields[s.Id]])),
            ],
            MaterialityBands =
            [
                .. (await Rows<MaterialityBand>(versionId).ToListAsync(cancellationToken).ConfigureAwait(false)).Select(b => new MaterialityBandEntry(
                    b.GovernanceProfileItemId, b.BandNo, b.CostThresholdPct, b.CostThresholdSar?.Amount, b.ScheduleThresholdPct, b.ScheduleThresholdDays,
                    b.ScopeRuleCode, b.RequiresApproval)),
            ],
            ProbabilityLevels = await Rows<ProbabilityLevelDefinition>(versionId)
                .Select(l => new ProbabilityLevelEntry(l.Level, l.Label, l.LowerPct, l.UpperPct))
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            ImpactLevels = await Rows<ImpactLevelDefinition>(versionId)
                .Select(l => new ImpactLevelEntry(l.ImpactDimensionItemId, l.Level, l.Label, l.Description, l.LowerBound, l.UpperBound))
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            RiskRatings = [.. ratings.Select(r => new RiskRatingEntry(r.Code, r.Label, r.SortOrder))],
            RiskMatrixCells =
            [
                .. (await Rows<RiskMatrixCell>(versionId).ToListAsync(cancellationToken).ConfigureAwait(false))
                    .Select(c => new RiskMatrixCellEntry(c.ProbabilityLevel, c.ImpactLevel, ratingCodes[c.RiskRatingDefinitionId])),
            ],
            ApprovalAuthority =
            [
                .. (await Rows<ApprovalAuthorityRule>(versionId).ToListAsync(cancellationToken).ConfigureAwait(false)).Select(r => new ApprovalAuthorityEntry(
                    r.SubjectTypeCode, r.GovernanceProfileItemId, r.BandNo, r.MinAmountSar?.Amount, r.SequenceNo, r.ApproverRoleId, r.IsMandatory)),
            ],
            NotificationEventFamilies =
            [
                .. eventFamilies.Select(f => new NotificationEventFamilyEntry(
                    f.Code, f.Label, f.IsMandatory,
                    [.. channels[f.Id].Select(c => new NotificationChannelEntry(c.Channel, c.EnabledByDefault))],
                    [.. recipients[f.Id]])),
            ],
            KpiPolicies = await Rows<KpiPolicyRule>(versionId)
                .Select(p => new KpiPolicyEntry(p.KpiDefinitionId, p.CalculationExpression, p.GreenThreshold, p.AmberThreshold))
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            ParticipationRules = await Rows<ParticipationContributionRule>(versionId)
                .Select(r => new ParticipationRuleEntry(r.ParticipationMode, r.ContributionTypeItemId, r.IsEnabled))
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            EvidenceRequirements = await Rows<EvidenceRequirementRule>(versionId)
                .Select(r => new EvidenceRequirementEntry(r.MilestoneCategoryItemId, r.EvidenceTypeItemId, r.IsMandatory))
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            FieldClassifications = await Rows<FieldClassificationRule>(versionId)
                .Select(r => new FieldClassificationEntry(r.EntityCode, r.FieldCode, r.DataClassificationItemId, r.MaskingRule))
                .ToListAsync(cancellationToken).ConfigureAwait(false),
            ReportFields = await Rows<ReportAllowlistEntry>(versionId)
                .Select(f => new ReportFieldEntry(f.SourceEntityCode, f.FieldCode, f.Label, f.IsFilterable, f.IsSortable, f.DataClassificationItemId))
                .ToListAsync(cancellationToken).ConfigureAwait(false),
        };
    }

    public void AddVersion(ConfigurationVersion version) => context.Set<ConfigurationVersion>().Add(version);

    /// <summary>
    /// The rows one level down (mandatory fields, channels, recipients) go with their parents: the foreign keys cascade
    /// in the database, and the content trigger lets them, since the version is DRAFT.
    /// </summary>
    public async Task StageContentAsync(Guid versionId, ConfigurationContent content, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        await RemoveAsync<ConfigurationValue>(versionId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync<GovernanceProfileSetting>(versionId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync<MaterialityBand>(versionId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync<ProbabilityLevelDefinition>(versionId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync<ImpactLevelDefinition>(versionId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync<RiskMatrixCell>(versionId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync<RiskRatingDefinition>(versionId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync<ApprovalAuthorityRule>(versionId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync<NotificationEventFamily>(versionId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync<KpiPolicyRule>(versionId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync<ParticipationContributionRule>(versionId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync<EvidenceRequirementRule>(versionId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync<FieldClassificationRule>(versionId, cancellationToken).ConfigureAwait(false);
        await RemoveAsync<ReportAllowlistEntry>(versionId, cancellationToken).ConfigureAwait(false);

        RowFactory rows = new(actorId, now);
        foreach (ConfigurationValueEntry value in content.Values)
        {
            Add(rows.Stamp(new ConfigurationValue { ConfigurationVersionId = versionId, ValueKey = value.Key, ValueText = value.Value, ValueType = value.Type }));
        }

        foreach (GovernanceProfileEntry profile in content.GovernanceProfiles)
        {
            GovernanceProfileSetting setting = rows.Stamp(new GovernanceProfileSetting
            {
                ConfigurationVersionId = versionId,
                GovernanceProfileItemId = profile.GovernanceProfileItemId,
                RequiresBaselineApproval = profile.RequiresBaselineApproval,
                RiskManagementRequired = profile.RiskManagementRequired,
                ChangeBandCount = profile.ChangeBandCount,
                UpdateCadenceDays = profile.UpdateCadenceDays,
                DocumentControlLevelItemId = profile.DocumentControlLevelItemId,
                IncludedInReporting = profile.IncludedInReporting,
                AssignmentMinBudgetSar = profile.AssignmentMinBudgetSar is { } budget ? new Money(budget) : null,
                AssignmentMinDurationDays = profile.AssignmentMinDurationDays,
            });
            Add(setting);
            foreach (string fieldCode in profile.MandatoryFieldCodes)
            {
                Add(rows.Stamp(new GovernanceProfileMandatoryField { GovernanceProfileSettingId = setting.Id, FieldCode = fieldCode }));
            }
        }

        foreach (MaterialityBandEntry band in content.MaterialityBands)
        {
            Add(rows.Stamp(new MaterialityBand
            {
                ConfigurationVersionId = versionId,
                GovernanceProfileItemId = band.GovernanceProfileItemId,
                BandNo = band.BandNo,
                CostThresholdPct = band.CostThresholdPct,
                CostThresholdSar = band.CostThresholdSar is { } cost ? new Money(cost) : null,
                ScheduleThresholdPct = band.ScheduleThresholdPct,
                ScheduleThresholdDays = band.ScheduleThresholdDays,
                ScopeRuleCode = band.ScopeRuleCode,
                RequiresApproval = band.RequiresApproval,
            }));
        }

        foreach (ProbabilityLevelEntry level in content.ProbabilityLevels)
        {
            Add(rows.Stamp(new ProbabilityLevelDefinition
            {
                ConfigurationVersionId = versionId,
                Level = level.Level,
                Label = level.Label,
                LowerPct = level.LowerPct,
                UpperPct = level.UpperPct,
            }));
        }

        foreach (ImpactLevelEntry level in content.ImpactLevels)
        {
            Add(rows.Stamp(new ImpactLevelDefinition
            {
                ConfigurationVersionId = versionId,
                ImpactDimensionItemId = level.ImpactDimensionItemId,
                Level = level.Level,
                Label = level.Label,
                Description = level.Description,
                LowerBound = level.LowerBound,
                UpperBound = level.UpperBound,
            }));
        }

        Dictionary<string, Guid> ratingIds = new(StringComparer.Ordinal);
        foreach (RiskRatingEntry rating in content.RiskRatings)
        {
            RiskRatingDefinition definition = rows.Stamp(new RiskRatingDefinition
            {
                ConfigurationVersionId = versionId,
                Code = rating.Code,
                Label = rating.Label,
                SortOrder = rating.SortOrder,
            });
            ratingIds[rating.Code] = definition.Id;
            Add(definition);
        }

        foreach (RiskMatrixCellEntry cell in content.RiskMatrixCells)
        {
            Add(rows.Stamp(new RiskMatrixCell
            {
                ConfigurationVersionId = versionId,
                ProbabilityLevel = cell.ProbabilityLevel,
                ImpactLevel = cell.ImpactLevel,
                RiskRatingDefinitionId = ratingIds[cell.RatingCode],
            }));
        }

        foreach (ApprovalAuthorityEntry rule in content.ApprovalAuthority)
        {
            Add(rows.Stamp(new ApprovalAuthorityRule
            {
                ConfigurationVersionId = versionId,
                SubjectTypeCode = rule.SubjectTypeCode,
                GovernanceProfileItemId = rule.GovernanceProfileItemId,
                BandNo = rule.BandNo,
                MinAmountSar = rule.MinAmountSar is { } amount ? new Money(amount) : null,
                SequenceNo = rule.SequenceNo,
                ApproverRoleId = rule.ApproverRoleId,
                IsMandatory = rule.IsMandatory,
            }));
        }

        foreach (NotificationEventFamilyEntry entry in content.NotificationEventFamilies)
        {
            NotificationEventFamily family = rows.Stamp(new NotificationEventFamily
            {
                ConfigurationVersionId = versionId,
                Code = entry.Code,
                Label = entry.Label,
                IsMandatory = entry.IsMandatory,
            });
            Add(family);
            foreach (NotificationChannelEntry channel in entry.Channels)
            {
                Add(rows.Stamp(new NotificationChannelRule { NotificationEventFamilyId = family.Id, Channel = channel.Channel, EnabledByDefault = channel.EnabledByDefault }));
            }

            foreach (Guid roleId in entry.RecipientRoleIds)
            {
                Add(rows.Stamp(new NotificationRecipientRule { NotificationEventFamilyId = family.Id, RoleId = roleId }));
            }
        }

        foreach (KpiPolicyEntry policy in content.KpiPolicies)
        {
            Add(rows.Stamp(new KpiPolicyRule
            {
                ConfigurationVersionId = versionId,
                KpiDefinitionId = policy.KpiDefinitionId,
                CalculationExpression = policy.CalculationExpression,
                GreenThreshold = policy.GreenThreshold,
                AmberThreshold = policy.AmberThreshold,
            }));
        }

        foreach (ParticipationRuleEntry rule in content.ParticipationRules)
        {
            Add(rows.Stamp(new ParticipationContributionRule
            {
                ConfigurationVersionId = versionId,
                ParticipationMode = rule.ParticipationMode,
                ContributionTypeItemId = rule.ContributionTypeItemId,
                IsEnabled = rule.IsEnabled,
            }));
        }

        foreach (EvidenceRequirementEntry rule in content.EvidenceRequirements)
        {
            Add(rows.Stamp(new EvidenceRequirementRule
            {
                ConfigurationVersionId = versionId,
                MilestoneCategoryItemId = rule.MilestoneCategoryItemId,
                EvidenceTypeItemId = rule.EvidenceTypeItemId,
                IsMandatory = rule.IsMandatory,
            }));
        }

        foreach (FieldClassificationEntry rule in content.FieldClassifications)
        {
            Add(rows.Stamp(new FieldClassificationRule
            {
                ConfigurationVersionId = versionId,
                EntityCode = rule.EntityCode,
                FieldCode = rule.FieldCode,
                DataClassificationItemId = rule.DataClassificationItemId,
                MaskingRule = rule.MaskingRule,
            }));
        }

        foreach (ReportFieldEntry field in content.ReportFields)
        {
            Add(rows.Stamp(new ReportAllowlistEntry
            {
                ConfigurationVersionId = versionId,
                SourceEntityCode = field.SourceEntityCode,
                FieldCode = field.FieldCode,
                Label = field.Label,
                IsFilterable = field.IsFilterable,
                IsSortable = field.IsSortable,
                DataClassificationItemId = field.DataClassificationItemId,
            }));
        }
    }

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken) => MasterDataConfigPersistence.SaveAsync(context, cancellationToken);

    /// <summary>The rows of a typed table that belong to the version, by its <c>configuration_version_id</c>.</summary>
    private IQueryable<TRow> Rows<TRow>(Guid versionId)
        where TRow : AuditedEntity =>
        context.Set<TRow>().AsNoTracking().Where(r => EF.Property<Guid>(r, nameof(ConfigurationValue.ConfigurationVersionId)) == versionId);

    private async Task RemoveAsync<TRow>(Guid versionId, CancellationToken cancellationToken)
        where TRow : AuditedEntity =>
        context.Set<TRow>().RemoveRange(await context.Set<TRow>()
            .Where(r => EF.Property<Guid>(r, nameof(ConfigurationValue.ConfigurationVersionId)) == versionId)
            .ToListAsync(cancellationToken).ConfigureAwait(false));

    private void Add<TRow>(TRow row)
        where TRow : AuditedEntity => context.Set<TRow>().Add(row);

    /// <summary>Gives each new row an id and the audit columns of the write that creates it.</summary>
    private sealed class RowFactory(Guid actorId, DateTimeOffset now)
    {
        public TRow Stamp<TRow>(TRow row)
            where TRow : AuditedEntity
        {
            row.Id = Guid.CreateVersion7(now);
            row.CreatedAt = now;
            row.CreatedBy = actorId;
            row.UpdatedAt = now;
            row.UpdatedBy = actorId;
            return row;
        }
    }
}
