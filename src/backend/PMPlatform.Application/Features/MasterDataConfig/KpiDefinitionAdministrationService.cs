using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Governance;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>
/// The KPI catalogue (TASK-034). A KPI definition is a stable identity under the governed lifecycle; its unit is a
/// PUBLISHED KPI_UNIT item. Once PUBLISHED, its unit and direction are fixed, because recorded values mean them.
/// </summary>
internal sealed partial class KpiDefinitionAdministrationService(
    IMasterDataRepository masterData,
    IAuditTrail audit,
    TimeProvider timeProvider,
    ILogger<KpiDefinitionAdministrationService> logger) : IKpiDefinitionAdministrationService
{
    public async Task<AdministrationResult<KpiDefinitionPage>> ListAsync(KpiDefinitionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await masterData.ListKpiDefinitionsAsync(query, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<KpiDefinitionDetail>>> GetAsync(Guid kpiDefinitionId, CancellationToken cancellationToken) =>
        await masterData.FindKpiDefinitionAsync(kpiDefinitionId, cancellationToken).ConfigureAwait(false) is { } definition ? definition : AdministrationError.NotFound;

    public async Task<AdministrationResult<Versioned<KpiDefinitionDetail>>> CreateAsync(Guid actorId, KpiDefinitionDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (await CheckUnitAsync(draft.UnitItemId, cancellationToken).ConfigureAwait(false) is { } unitIssue)
        {
            return AdministrationError.Rule(MasterDataConfigErrorCodes.ReferenceInvalid, unitIssue);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        KpiDefinition definition = new()
        {
            Id = Guid.CreateVersion7(now),
            Code = draft.Code,
            Name = draft.Name,
            Description = draft.Description,
            UnitItemId = draft.UnitItemId,
            Direction = draft.Direction,
            LifecycleState = GovernedLifecycleState.Draft,
            CreatedAt = now,
            CreatedBy = actorId,
        };
        masterData.AddKpiDefinition(definition);
        return await SaveAsync(actorId, definition, "created", MasterDataConfigAuditEvents.KpiDefinitionCreated,
            [
                AuditAttribute.Change("code", null, definition.Code),
                AuditAttribute.Change("name_ar", null, definition.Name.Ar),
                AuditAttribute.Change("name_en", null, definition.Name.En),
                AuditAttribute.Change("description_ar", null, definition.Description?.Ar),
                AuditAttribute.Change("description_en", null, definition.Description?.En),
                AuditAttribute.Change("unit_item_id", null, definition.UnitItemId),
                AuditAttribute.Change("direction", null, definition.Direction),
                AuditAttribute.Change("lifecycle_state", null, definition.LifecycleState),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<KpiDefinitionDetail>>> UpdateAsync(
        Guid actorId, Guid kpiDefinitionId, KpiDefinitionChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        KpiDefinition? definition = await masterData.FindKpiDefinitionForUpdateAsync(kpiDefinitionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return AdministrationError.NotFound;
        }

        if (definition.LifecycleState == GovernedLifecycleState.Published)
        {
            List<FieldIssue> fixedFields = [];
            if (changes.UnitItemId != definition.UnitItemId)
            {
                fixedFields.Add(new FieldIssue("unitItemId", FieldIssue.NotAllowed));
            }

            if (changes.Direction != definition.Direction)
            {
                fixedFields.Add(new FieldIssue("direction", FieldIssue.NotAllowed));
            }

            if (fixedFields.Count > 0)
            {
                return AdministrationError.Rule(MasterDataConfigErrorCodes.PublishedImmutable, [.. fixedFields]);
            }
        }
        else if (GovernedRefusal.Of(GovernedLifecycle.CheckDraftEdit(definition, actorId)) is { } refused)
        {
            return refused;
        }
        else if (changes.UnitItemId != definition.UnitItemId && await CheckUnitAsync(changes.UnitItemId, cancellationToken).ConfigureAwait(false) is { } unitIssue)
        {
            return AdministrationError.Rule(MasterDataConfigErrorCodes.ReferenceInvalid, unitIssue);
        }

        AuditAttribute?[] changed =
        [
            AuditAttribute.Change("name_ar", definition.Name.Ar, changes.Name.Ar),
            AuditAttribute.Change("name_en", definition.Name.En, changes.Name.En),
            AuditAttribute.Change("description_ar", definition.Description?.Ar, changes.Description?.Ar),
            AuditAttribute.Change("description_en", definition.Description?.En, changes.Description?.En),
            AuditAttribute.Change("unit_item_id", definition.UnitItemId, changes.UnitItemId),
            AuditAttribute.Change("direction", definition.Direction, changes.Direction),
        ];
        definition.Name = changes.Name;
        definition.Description = changes.Description;
        definition.UnitItemId = changes.UnitItemId;
        definition.Direction = changes.Direction;
        return await SaveAsync(actorId, definition, "updated", MasterDataConfigAuditEvents.KpiDefinitionUpdated, changed, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<KpiDefinitionDetail>>> ValidateAsync(
        Guid actorId, Guid kpiDefinitionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        KpiDefinition? definition = await masterData.FindKpiDefinitionForUpdateAsync(kpiDefinitionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        return definition is null
            ? (AdministrationResult<Versioned<KpiDefinitionDetail>>)AdministrationError.NotFound
            : GovernedRefusal.Of(GovernedLifecycle.Validate(definition, actorId, timeProvider.GetUtcNow())) is { } refused
            ? refused
            : await SaveAsync(actorId, definition, "validated", MasterDataConfigAuditEvents.KpiDefinitionValidated,
                [AuditAttribute.Change("lifecycle_state", GovernedLifecycleState.Draft, definition.LifecycleState)], cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<KpiDefinitionDetail>>> PublishAsync(
        Guid actorId, Guid kpiDefinitionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        KpiDefinition? definition = await masterData.FindKpiDefinitionForUpdateAsync(kpiDefinitionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return AdministrationError.NotFound;
        }

        if (GovernedRefusal.Of(GovernedLifecycle.CheckPublish(definition, actorId)) is { } refused)
        {
            return refused;
        }

        if (await CheckUnitAsync(definition.UnitItemId, cancellationToken).ConfigureAwait(false) is { } unitIssue)
        {
            return AdministrationError.Rule(MasterDataConfigErrorCodes.ReferenceInvalid, unitIssue);
        }

        GovernedLifecycle.Publish(definition, actorId, timeProvider.GetUtcNow());
        return await SaveAsync(actorId, definition, "published", MasterDataConfigAuditEvents.KpiDefinitionPublished,
            [AuditAttribute.Change("lifecycle_state", GovernedLifecycleState.Validated, definition.LifecycleState)], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>KPI_POLICY versions already published keep their rules for it; a new version can no longer reference it.</summary>
    public async Task<AdministrationResult<Versioned<KpiDefinitionDetail>>> RetireAsync(
        Guid actorId, Guid kpiDefinitionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        KpiDefinition? definition = await masterData.FindKpiDefinitionForUpdateAsync(kpiDefinitionId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return AdministrationError.NotFound;
        }

        GovernedLifecycleState before = definition.LifecycleState;
        return GovernedRefusal.Of(GovernedLifecycle.Retire(definition, timeProvider.GetUtcNow())) is { } refused
            ? refused
            : await SaveAsync(actorId, definition, "retired", MasterDataConfigAuditEvents.KpiDefinitionRetired,
                [AuditAttribute.Change("lifecycle_state", before, definition.LifecycleState)], cancellationToken).ConfigureAwait(false);
    }

    private async Task<FieldIssue?> CheckUnitAsync(Guid unitItemId, CancellationToken cancellationToken)
    {
        ItemFacts? unit = (await masterData.FindItemFactsAsync([unitItemId], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(unitItemId);
        return unit is null ? new FieldIssue("unitItemId", FieldIssue.NotFound)
            : unit.CatalogueCode != MasterDataCatalogueCodes.KpiUnit ? new FieldIssue("unitItemId", ContentIssueCodes.WrongCatalogue)
            : unit.LifecycleState != GovernedLifecycleState.Published ? new FieldIssue("unitItemId", ContentIssueCodes.NotPublished)
            : null;
    }

    private async Task<AdministrationResult<Versioned<KpiDefinitionDetail>>> SaveAsync(
        Guid actorId, KpiDefinition definition, string change, string eventType, AuditAttribute?[] attributes, CancellationToken cancellationToken)
    {
        definition.UpdatedAt = timeProvider.GetUtcNow();
        definition.UpdatedBy = actorId;
        audit.Stage(ConfigurationAudit.Entry(eventType, actorId, nameof(KpiDefinition), definition.Id, attributes));
        if ((await masterData.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogChanged(logger, actorId, change, definition.Id);
        return await masterData.FindKpiDefinitionAsync(definition.Id, cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException($"KPI definition {definition.Id} was saved and cannot be read back.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "KPI catalogue administration: {ActorId} {Change} KPI definition {KpiDefinitionId}.")]
    private static partial void LogChanged(ILogger logger, Guid actorId, string change, Guid kpiDefinitionId);
}
