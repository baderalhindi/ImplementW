using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Governance;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// ADM-037 (FG-02 §11; TASK-071) on the platform's governed lifecycle primitive (M-10), as ADM-036 is: a report version is authored, reviewed and
/// published by three people (<see cref="GovernedLifecycle"/>). Validation runs at validation and again at publication, against FG-01's register
/// in force then (US-RPT-SYS-054); publishing retires the version it replaces in the same save, and nothing retires the one PUBLISHED version of a
/// report alone, so the ten reports of ADR-006 stay ten.
/// </summary>
internal sealed class ReportDefinitionService(
    IReportRepository repository,
    IRoleDirectory roles,
    ReportFields fields,
    IAuditTrail audit,
    TimeProvider timeProvider) : IReportDefinitionService
{
    public async Task<ReportDefinitionPage> ListAsync(ReportDefinitionQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        (IReadOnlyList<ReportDefinition> items, int total) = await repository.PageDefinitionsAsync(query, page, cancellationToken).ConfigureAwait(false);
        return new ReportDefinitionPage(
            [.. items.Select(d => new ReportDefinitionSummary(d.Id, d.Code, d.VersionNo, d.Name, d.LifecycleState, d.PublishedAt, d.RetiredAt, d.UpdatedAt))],
            page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ReportDefinitionDetail>>> GetAsync(Guid definitionId, CancellationToken cancellationToken) =>
        await repository.FindDefinitionAsync(definitionId, null, cancellationToken).ConfigureAwait(false) is { } definition
            ? await VersionedAsync(definition, cancellationToken).ConfigureAwait(false)
            : AdministrationError.NotFound;

    public async Task<AdministrationResult<Versioned<ReportDefinitionDetail>>> CreateAsync(Guid actorId, ReportCode code, CancellationToken cancellationToken)
    {
        if (await repository.FindOpenAsync(code, cancellationToken).ConfigureAwait(false) is not null)
        {
            return AdministrationError.Conflict(ReportErrorCodes.VersionOpen);
        }

        // The next version starts as a copy of the one in force, so a change is made against what users run now.
        ReportDefinition? current = await repository.FindPublishedAsync(code, track: false, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        ReportDefinition draft = new()
        {
            Id = Guid.CreateVersion7(now),
            Code = code,
            VersionNo = await repository.MaxVersionNoAsync(code, cancellationToken).ConfigureAwait(false) + 1,
            Name = current?.Name ?? new BilingualLabel(code.ToString(), code.ToString()),
            Description = current?.Description,
            AudienceFamily = current?.AudienceFamily ?? ReportAudienceFamily.Portfolio,
            PrimaryProjectionCode = current?.PrimaryProjectionCode ?? ReportFields.ExplorerBasis,
            AllowsSavedViews = current?.AllowsSavedViews ?? false,
            LifecycleState = GovernedLifecycleState.Draft,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        repository.Add(draft);
        int columnCount = 0;
        if (current is not null)
        {
            IReadOnlyDictionary<Guid, string> roleCodes = await RoleCodesAsync(cancellationToken).ConfigureAwait(false);
            ReportDefinitionContent copy = await ContentAsync(current, roleCodes, cancellationToken).ConfigureAwait(false);
            Stage(draft, copy, roleCodes.ToDictionary(r => r.Value, r => r.Key, StringComparer.Ordinal), actorId, now);
            columnCount = copy.Columns.Count;
        }

        audit.Stage(ReportAudit.DefinitionCreated(actorId, draft, columnCount));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            ReportSaveOutcome.Saved => await VersionedAsync(draft, cancellationToken).ConfigureAwait(false),
            ReportSaveOutcome.Duplicate => AdministrationError.Conflict(ReportErrorCodes.VersionOpen),
            ReportSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<Versioned<ReportDefinitionDetail>>> UpdateAsync(
        Guid actorId, Guid definitionId, ReportDefinitionContent content, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (await repository.FindDefinitionAsync(definitionId, expectedVersion, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        if (Refusal(GovernedLifecycle.CheckDraftEdit(definition, actorId)) is { } refused)
        {
            return refused;
        }

        // The roles must exist; the rest of §11.2's validation is the reviewer's step, so a DRAFT may be saved incomplete.
        Dictionary<string, Guid> roleIds = (await RoleCodesAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.Value, r => r.Key, StringComparer.Ordinal);
        List<FieldIssue> issues = [.. content.AudienceRoleCodes.Select((role, i) => (role, i)).Where(x => !roleIds.ContainsKey(x.role))
            .Select(x => new FieldIssue($"audienceRoleCodes[{x.i}]", FieldIssue.NotFound))];
        if (content.AudienceRoleCodes.Distinct(StringComparer.Ordinal).Count() != content.AudienceRoleCodes.Count)
        {
            issues.Add(new FieldIssue("audienceRoleCodes", FieldIssue.Duplicate));
        }

        if (content.Columns.Select(c => $"{c.SourceEntityCode}.{c.FieldCode}").Distinct(StringComparer.Ordinal).Count() != content.Columns.Count)
        {
            issues.Add(new FieldIssue("columns", FieldIssue.Duplicate));
        }

        if (content.Parameters.Select(p => p.Code).Distinct(StringComparer.Ordinal).Count() != content.Parameters.Count)
        {
            issues.Add(new FieldIssue("parameters", FieldIssue.Duplicate));
        }

        if (issues.Count > 0)
        {
            return AdministrationError.Rule(ReportErrorCodes.DefinitionInvalid, [.. issues]);
        }

        foreach (ReportColumn column in await repository.ListColumnsAsync(definition.Id, track: true, cancellationToken).ConfigureAwait(false))
        {
            repository.Remove(column);
        }

        foreach (ReportAudienceRole member in await repository.ListAudienceAsync([definition.Id], track: true, cancellationToken).ConfigureAwait(false))
        {
            repository.Remove(member);
        }

        IReadOnlyList<ReportParameter> parameters = await repository.ListParametersAsync(definition.Id, track: true, cancellationToken).ConfigureAwait(false);
        foreach (ReportParameterOption option in await repository.ListOptionsAsync([.. parameters.Select(p => p.Id)], track: true, cancellationToken).ConfigureAwait(false))
        {
            repository.Remove(option);
        }

        foreach (ReportParameter parameter in parameters)
        {
            repository.Remove(parameter);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Stage(definition, content, roleIds, actorId, now);
        definition.Name = content.Name;
        definition.Description = content.Description;
        definition.AudienceFamily = content.AudienceFamily;
        definition.PrimaryProjectionCode = content.PrimaryProjectionCode;
        definition.AllowsSavedViews = content.AllowsSavedViews;
        Touch(definition, actorId, now);
        audit.Stage(ReportAudit.DefinitionChanged(actorId, definition, content.Columns.Count, content.Parameters.Count, content.AudienceRoleCodes));
        return await SaveAsync(definition, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ReportDefinitionDetail>>> ValidateAsync(Guid actorId, Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        if (await repository.FindDefinitionAsync(definitionId, expectedVersion, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        if (Refusal(GovernedLifecycle.CheckValidate(definition, actorId)) is { } refused)
        {
            return refused;
        }

        if (await IssuesAsync(definition, cancellationToken).ConfigureAwait(false) is { Count: > 0 } issues)
        {
            return AdministrationError.Rule(ReportErrorCodes.DefinitionInvalid, [.. issues]);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        GovernedLifecycle.Validate(definition, actorId, now);
        Touch(definition, actorId, now);
        audit.Stage(ReportAudit.DefinitionValidated(actorId, definition));
        return await SaveAsync(definition, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ReportDefinitionDetail>>> PublishAsync(Guid actorId, Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        if (await repository.FindDefinitionAsync(definitionId, expectedVersion, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        if (Refusal(GovernedLifecycle.CheckPublish(definition, actorId)) is { } refused)
        {
            return refused;
        }

        if (await IssuesAsync(definition, cancellationToken).ConfigureAwait(false) is { Count: > 0 } issues)
        {
            return AdministrationError.Rule(ReportErrorCodes.DefinitionInvalid, [.. issues]);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ReportDefinition? replaced = await repository.FindPublishedAsync(definition.Code, track: true, cancellationToken).ConfigureAwait(false);
        GovernedLifecycle.Publish(definition, actorId, now);
        Touch(definition, actorId, now);
        audit.Stage(ReportAudit.DefinitionPublished(actorId, definition, replaced?.Id));
        if (replaced is not null)
        {
            GovernedLifecycle.Retire(replaced, now);
            Touch(replaced, actorId, now);
            audit.Stage(ReportAudit.DefinitionRetired(actorId, replaced, GovernedLifecycleState.Published, definition.Id));
        }

        return await SaveAsync(definition, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ReportDefinitionDetail>>> RetireAsync(Guid actorId, Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        if (await repository.FindDefinitionAsync(definitionId, expectedVersion, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        if (definition.LifecycleState == GovernedLifecycleState.Published)
        {
            return AdministrationError.Conflict(ReportErrorCodes.RetirementNotPermitted);
        }

        GovernedLifecycleState from = definition.LifecycleState;
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (Refusal(GovernedLifecycle.Retire(definition, now)) is { } refused)
        {
            return refused;
        }

        Touch(definition, actorId, now);
        audit.Stage(ReportAudit.DefinitionRetired(actorId, definition, from, null));
        return await SaveAsync(definition, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<FieldIssue>> IssuesAsync(ReportDefinition definition, CancellationToken cancellationToken) =>
        ReportDefinitionRules.Check(definition.Code, await ContentAsync(definition, await RoleCodesAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false), fields);

    /// <summary>A stored version's content, whole, as ADM-037 edits it.</summary>
    private async Task<ReportDefinitionContent> ContentAsync(ReportDefinition definition, IReadOnlyDictionary<Guid, string> roleCodes, CancellationToken cancellationToken)
    {
        IReadOnlyList<ReportColumn> columns = await repository.ListColumnsAsync(definition.Id, track: false, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ReportAudienceRole> audience = await repository.ListAudienceAsync([definition.Id], track: false, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ReportParameter> parameters = await repository.ListParametersAsync(definition.Id, track: false, cancellationToken).ConfigureAwait(false);
        ILookup<Guid, ReportParameterOption> options = (await repository.ListOptionsAsync([.. parameters.Select(p => p.Id)], track: false, cancellationToken).ConfigureAwait(false))
            .ToLookup(o => o.ReportParameterId);
        return new ReportDefinitionContent(
            definition.Name,
            definition.Description,
            definition.AudienceFamily,
            definition.PrimaryProjectionCode,
            definition.AllowsSavedViews,
            [.. audience.Select(a => roleCodes[a.RoleId]).Order(StringComparer.Ordinal)],
            [.. parameters.OrderBy(p => p.SortOrder).Select(p => new ReportParameterDefinitionInput(
                p.Code, p.Label, p.DataType, p.IsRequired, p.SourceEntityCode, p.FieldCode,
                [.. options[p.Id].OrderBy(o => o.ValueCode, StringComparer.Ordinal).Select(o => new ReportParameterOptionInput(o.ValueCode, o.Label, o.CatalogueEntryReference))]))],
            [.. columns.OrderBy(c => c.SortOrder).Select(c => new ReportColumnInput(c.SourceEntityCode, c.FieldCode, c.Label, c.IsDefaultVisible, c.DataClassificationItemId))]);
    }

    /// <summary>Adds a version's columns, audience, parameters and options, numbered in the content's order.</summary>
    private void Stage(ReportDefinition definition, ReportDefinitionContent content, Dictionary<string, Guid> roleIds, Guid actorId, DateTimeOffset now)
    {
        short order = 0;
        foreach (ReportColumnInput c in content.Columns)
        {
            repository.Add(new ReportColumn
            {
                Id = Guid.CreateVersion7(now),
                ReportDefinitionId = definition.Id,
                SourceEntityCode = c.SourceEntityCode,
                FieldCode = c.FieldCode,
                Label = c.Label,
                SortOrder = ++order,
                IsDefaultVisible = c.IsDefaultVisible,
                DataClassificationItemId = c.DataClassificationItemId,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }

        foreach (string role in content.AudienceRoleCodes)
        {
            repository.Add(new ReportAudienceRole
            {
                Id = Guid.CreateVersion7(now),
                ReportDefinitionId = definition.Id,
                RoleId = roleIds[role],
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }

        order = 0;
        foreach (ReportParameterDefinitionInput p in content.Parameters)
        {
            ReportParameter parameter = new()
            {
                Id = Guid.CreateVersion7(now),
                ReportDefinitionId = definition.Id,
                Code = p.Code,
                Label = p.Label,
                DataType = p.DataType,
                IsRequired = p.IsRequired,
                SortOrder = ++order,
                SourceEntityCode = p.SourceEntityCode,
                FieldCode = p.FieldCode,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            };
            repository.Add(parameter);
            foreach (ReportParameterOptionInput o in p.Options)
            {
                repository.Add(new ReportParameterOption
                {
                    Id = Guid.CreateVersion7(now),
                    ReportParameterId = parameter.Id,
                    ValueCode = o.ValueCode,
                    Label = o.Label,
                    CatalogueEntryReference = o.CatalogueEntryReference,
                    CreatedAt = now,
                    CreatedBy = actorId,
                    UpdatedAt = now,
                    UpdatedBy = actorId,
                });
            }
        }
    }

    private async Task<AdministrationResult<Versioned<ReportDefinitionDetail>>> SaveAsync(ReportDefinition definition, CancellationToken cancellationToken) =>
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            ReportSaveOutcome.Saved => await VersionedAsync(definition, cancellationToken).ConfigureAwait(false),

            // At commit: another version of the report was published by a request that saved first.
            ReportSaveOutcome.Duplicate or ReportSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };

    private async Task<Versioned<ReportDefinitionDetail>> VersionedAsync(ReportDefinition definition, CancellationToken cancellationToken)
    {
        ReportDefinitionContent content = await ContentAsync(definition, await RoleCodesAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return new(
            new ReportDefinitionDetail(
                definition.Id, definition.Code, definition.VersionNo, content.Name, content.Description, content.AudienceFamily, content.PrimaryProjectionCode,
                content.AllowsSavedViews, definition.LifecycleState, definition.ValidatedByUserId, definition.ValidatedAt, definition.PublishedByUserId, definition.PublishedAt,
                definition.RetiredAt, content.AudienceRoleCodes, content.Parameters, content.Columns, ReportCatalogue.CatalogueEntries[definition.Code],
                definition.CreatedAt, definition.CreatedBy, definition.UpdatedAt, definition.UpdatedBy),
            repository.RowVersionOf(definition));
    }

    private async Task<IReadOnlyDictionary<Guid, string>> RoleCodesAsync(CancellationToken cancellationToken) =>
        (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.Id, r => r.Code);

    private static AdministrationError? Refusal(GovernedTransitionOutcome outcome) => outcome switch
    {
        GovernedTransitionOutcome.Applied => null,
        GovernedTransitionOutcome.InvalidTransition => AdministrationError.InvalidTransition,
        GovernedTransitionOutcome.TerminalState => AdministrationError.TerminalState,
        GovernedTransitionOutcome.SeparationOfDuties => AdministrationError.Rule(ReportErrorCodes.SeparationOfDuties),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown transition outcome."),
    };

    private static void Touch(ReportDefinition definition, Guid actorId, DateTimeOffset now)
    {
        definition.UpdatedAt = now;
        definition.UpdatedBy = actorId;
    }
}
