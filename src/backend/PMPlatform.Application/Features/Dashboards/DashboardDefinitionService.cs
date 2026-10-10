using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Governance;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>
/// ADM-036 (FG-01 §13, TASK-069) on the platform's governed lifecycle primitive (M-10), so a dashboard version is authored, reviewed and
/// published by three people as every configuration is (<see cref="GovernedLifecycle"/>). Validation is run at validation and again at
/// publication, against the projection register in force then (BR-DSH-050); publishing retires the version it replaces in the same save,
/// and nothing retires the one PUBLISHED version of a dashboard alone, so the three dashboards of ADR-006 stay three.
/// </summary>
internal sealed class DashboardDefinitionService(
    IDashboardRepository repository,
    IRoleDirectory roles,
    IAuditTrail audit,
    TimeProvider timeProvider) : IDashboardDefinitionService
{
    public async Task<DashboardDefinitionPage> ListAsync(DashboardDefinitionQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        (IReadOnlyList<DashboardDefinition> items, int total) = await repository.PageAsync(query, page, cancellationToken).ConfigureAwait(false);
        return new DashboardDefinitionPage([.. items.Select(DashboardMapping.ToSummary)], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<DashboardDefinitionDetail>>> GetAsync(Guid definitionId, CancellationToken cancellationToken) =>
        await repository.FindAsync(definitionId, null, cancellationToken).ConfigureAwait(false) is { } definition
            ? await VersionedAsync(definition, cancellationToken).ConfigureAwait(false)
            : AdministrationError.NotFound;

    public async Task<AdministrationResult<Versioned<DashboardDefinitionDetail>>> CreateAsync(Guid actorId, DashboardCode code, CancellationToken cancellationToken)
    {
        if (await repository.FindOpenAsync(code, cancellationToken).ConfigureAwait(false) is not null)
        {
            return AdministrationError.Conflict(DashboardErrorCodes.VersionOpen);
        }

        // The next version starts as a copy of the one in force, so a change is made against what users see now.
        DashboardDefinition? current = await repository.FindPublishedAsync(code, track: false, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        DashboardDefinition draft = new()
        {
            Id = Guid.CreateVersion7(now),
            Code = code,
            VersionNo = await repository.MaxVersionNoAsync(code, cancellationToken).ConfigureAwait(false) + 1,
            Name = current?.Name ?? new BilingualLabel(code.ToString(), code.ToString()),
            Description = current?.Description,
            AllowsPersonalization = current?.AllowsPersonalization ?? false,
            LifecycleState = GovernedLifecycleState.Draft,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        repository.Add(draft);

        List<DashboardWidget> widgets = [];
        if (current is not null)
        {
            foreach (DashboardWidget w in await repository.ListWidgetsAsync(current.Id, track: false, cancellationToken).ConfigureAwait(false))
            {
                DashboardWidget copy = DashboardMapping.ToWidget(DashboardMapping.ToInput(w), draft.Id, actorId, now);
                widgets.Add(copy);
                repository.Add(copy);
            }

            foreach (DashboardAudienceRole a in await repository.ListAudienceAsync([current.Id], track: false, cancellationToken).ConfigureAwait(false))
            {
                repository.Add(DashboardMapping.ToAudience(a.RoleId, a.IsDefaultLanding, draft.Id, actorId, now));
            }
        }

        audit.Stage(DashboardAudit.Created(actorId, draft, widgets));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            DashboardSaveOutcome.Saved => await VersionedAsync(draft, cancellationToken).ConfigureAwait(false),
            DashboardSaveOutcome.Duplicate => AdministrationError.Conflict(DashboardErrorCodes.VersionOpen),
            DashboardSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<Versioned<DashboardDefinitionDetail>>> UpdateAsync(
        Guid actorId, Guid definitionId, DashboardDefinitionContent content, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (await repository.FindAsync(definitionId, expectedVersion, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        if (Refusal(GovernedLifecycle.CheckDraftEdit(definition, actorId)) is { } refused)
        {
            return refused;
        }

        // The codes the content names must be roles; ADR-019's personalisation rule is the table's own CHECK, so it is answered here.
        Dictionary<string, Guid> roleIds = (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.Code, r => r.Id, StringComparer.Ordinal);
        List<FieldIssue> issues = [.. content.Audience.Select((a, i) => (a, i)).Where(x => !roleIds.ContainsKey(x.a.RoleCode))
            .Select(x => new FieldIssue($"audience[{x.i}].roleCode", FieldIssue.NotFound))];
        if (content.AllowsPersonalization && !DashboardCatalogue.MayPersonalize(definition.Code))
        {
            issues.Add(new FieldIssue("allowsPersonalization", FieldIssue.NotAllowed));
        }

        if (content.Audience.Select(a => a.RoleCode).Distinct(StringComparer.Ordinal).Count() != content.Audience.Count)
        {
            issues.Add(new FieldIssue("audience", FieldIssue.Duplicate));
        }

        if (content.Widgets.Select(w => w.Code).Distinct(StringComparer.Ordinal).Count() != content.Widgets.Count)
        {
            issues.Add(new FieldIssue("widgets", FieldIssue.Duplicate));
        }

        if (issues.Count > 0)
        {
            return AdministrationError.Rule(DashboardErrorCodes.DefinitionInvalid, [.. issues]);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (DashboardWidget w in await repository.ListWidgetsAsync(definition.Id, track: true, cancellationToken).ConfigureAwait(false))
        {
            repository.Remove(w);
        }

        foreach (DashboardAudienceRole a in await repository.ListAudienceAsync([definition.Id], track: true, cancellationToken).ConfigureAwait(false))
        {
            repository.Remove(a);
        }

        List<DashboardWidget> widgets = [.. content.Widgets.Select(w => DashboardMapping.ToWidget(w, definition.Id, actorId, now))];
        widgets.ForEach(repository.Add);
        foreach (DashboardAudienceInput a in content.Audience)
        {
            repository.Add(DashboardMapping.ToAudience(roleIds[a.RoleCode], a.IsDefaultLanding, definition.Id, actorId, now));
        }

        definition.Name = content.Name;
        definition.Description = content.Description;
        definition.AllowsPersonalization = content.AllowsPersonalization;
        Touch(definition, actorId, now);
        audit.Stage(DashboardAudit.Changed(actorId, definition, widgets, [.. content.Audience.Select(a => a.RoleCode)]));
        return await SaveAsync(definition, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<DashboardDefinitionDetail>>> ValidateAsync(Guid actorId, Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        if (await repository.FindAsync(definitionId, expectedVersion, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        if (Refusal(GovernedLifecycle.CheckValidate(definition, actorId)) is { } refused)
        {
            return refused;
        }

        if (await IssuesAsync(definition, cancellationToken).ConfigureAwait(false) is { Count: > 0 } issues)
        {
            return AdministrationError.Rule(DashboardErrorCodes.DefinitionInvalid, [.. issues]);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        GovernedLifecycle.Validate(definition, actorId, now);
        Touch(definition, actorId, now);
        audit.Stage(DashboardAudit.Validated(actorId, definition));
        return await SaveAsync(definition, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<DashboardDefinitionDetail>>> PublishAsync(Guid actorId, Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        if (await repository.FindAsync(definitionId, expectedVersion, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        if (Refusal(GovernedLifecycle.CheckPublish(definition, actorId)) is { } refused)
        {
            return refused;
        }

        if (await IssuesAsync(definition, cancellationToken).ConfigureAwait(false) is { Count: > 0 } issues)
        {
            return AdministrationError.Rule(DashboardErrorCodes.DefinitionInvalid, [.. issues]);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        DashboardDefinition? replaced = await repository.FindPublishedAsync(definition.Code, track: true, cancellationToken).ConfigureAwait(false);
        GovernedLifecycle.Publish(definition, actorId, now);
        Touch(definition, actorId, now);
        audit.Stage(DashboardAudit.Published(actorId, definition, replaced?.Id));
        if (replaced is not null)
        {
            GovernedLifecycle.Retire(replaced, now);
            Touch(replaced, actorId, now);
            audit.Stage(DashboardAudit.Retired(actorId, replaced, GovernedLifecycleState.Published, definition.Id));
        }

        return await SaveAsync(definition, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<DashboardDefinitionDetail>>> RetireAsync(Guid actorId, Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        if (await repository.FindAsync(definitionId, expectedVersion, cancellationToken).ConfigureAwait(false) is not { } definition)
        {
            return AdministrationError.NotFound;
        }

        if (definition.LifecycleState == GovernedLifecycleState.Published)
        {
            return AdministrationError.Conflict(DashboardErrorCodes.RetirementNotPermitted);
        }

        GovernedLifecycleState from = definition.LifecycleState;
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (Refusal(GovernedLifecycle.Retire(definition, now)) is { } refused)
        {
            return refused;
        }

        Touch(definition, actorId, now);
        audit.Stage(DashboardAudit.Retired(actorId, definition, from, null));
        return await SaveAsync(definition, cancellationToken).ConfigureAwait(false);
    }

    public DashboardProjectionPage ListProjections(PageRequest page)
    {
        ArgumentNullException.ThrowIfNull(page);
        List<DashboardProjectionDetail> all = [.. DashboardProjections.All.OrderBy(p => p.Code, StringComparer.Ordinal).Select(p => p.ToDetail())];
        return new DashboardProjectionPage([.. all.Skip(page.Skip).Take(page.PageSize)], page.Page, page.PageSize, all.Count);
    }

    /// <summary>The stored content's validation, and at publication the default landings other PUBLISHED dashboards hold.</summary>
    private async Task<IReadOnlyList<FieldIssue>> IssuesAsync(DashboardDefinition definition, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, string> roleCodes = await RoleCodesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<DashboardWidget> widgets = await repository.ListWidgetsAsync(definition.Id, track: false, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<DashboardAudienceRole> audience = await repository.ListAudienceAsync([definition.Id], track: false, cancellationToken).ConfigureAwait(false);
        DashboardDefinitionContent content = DashboardMapping.ToContent(definition, audience, widgets, roleCodes);
        List<FieldIssue> issues = [.. DashboardDefinitionRules.Check(definition.Code, content)];
        if (definition.LifecycleState == GovernedLifecycleState.Validated)
        {
            List<Guid> others = [.. (await repository.ListPublishedAsync(cancellationToken).ConfigureAwait(false)).Where(d => d.Code != definition.Code).Select(d => d.Id)];
            HashSet<string> landing = [.. (await repository.ListAudienceAsync(others, track: false, cancellationToken).ConfigureAwait(false))
                .Where(a => a.IsDefaultLanding).Select(a => roleCodes[a.RoleId])];
            issues.AddRange(DashboardDefinitionRules.CheckLandings(content, landing));
        }

        return issues;
    }

    private async Task<AdministrationResult<Versioned<DashboardDefinitionDetail>>> SaveAsync(DashboardDefinition definition, CancellationToken cancellationToken) =>
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            DashboardSaveOutcome.Saved => await VersionedAsync(definition, cancellationToken).ConfigureAwait(false),

            // At commit: another version of the dashboard was published, or a default landing was taken, by a request that saved first.
            DashboardSaveOutcome.Duplicate or DashboardSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };

    private async Task<Versioned<DashboardDefinitionDetail>> VersionedAsync(DashboardDefinition definition, CancellationToken cancellationToken)
    {
        IReadOnlyList<DashboardWidget> widgets = await repository.ListWidgetsAsync(definition.Id, track: false, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<DashboardAudienceRole> audience = await repository.ListAudienceAsync([definition.Id], track: false, cancellationToken).ConfigureAwait(false);
        return new(DashboardMapping.ToDetail(definition, audience, widgets, await RoleCodesAsync(cancellationToken).ConfigureAwait(false)), repository.RowVersionOf(definition));
    }

    private async Task<IReadOnlyDictionary<Guid, string>> RoleCodesAsync(CancellationToken cancellationToken) =>
        (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.Id, r => r.Code);

    private static AdministrationError? Refusal(GovernedTransitionOutcome outcome) => outcome switch
    {
        GovernedTransitionOutcome.Applied => null,
        GovernedTransitionOutcome.InvalidTransition => AdministrationError.InvalidTransition,
        GovernedTransitionOutcome.TerminalState => AdministrationError.TerminalState,
        GovernedTransitionOutcome.SeparationOfDuties => AdministrationError.Rule(DashboardErrorCodes.SeparationOfDuties),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown transition outcome."),
    };

    private static void Touch(DashboardDefinition definition, Guid actorId, DateTimeOffset now)
    {
        definition.UpdatedAt = now;
        definition.UpdatedBy = actorId;
    }
}
