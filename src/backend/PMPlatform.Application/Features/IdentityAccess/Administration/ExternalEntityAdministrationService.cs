using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// ADM-013 (TASK-031, ADR-013). ACTIVE ↔ SUSPENDED, either → RETIRED, and RETIRED takes no further write. The status alone
/// decides whether the entity's people may sign in and act (sign-in and the engine both read it); their assignments and
/// the records they touched are not rewritten.
/// </summary>
internal sealed partial class ExternalEntityAdministrationService(
    IExternalEntityRepository entities,
    IUserAdministrationRepository users,
    AdministrationAccess access,
    TimeProvider timeProvider,
    ILogger<ExternalEntityAdministrationService> logger) : IExternalEntityAdministrationService
{
    public async Task<AdministrationResult<ExternalEntityPage>> ListAsync(Guid actorId, ExternalEntityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await access.CheckCollectionAsync(actorId, PermissionCatalogue.OrganizationView, cancellationToken).ConfigureAwait(false) is { } denied
            ? denied
            : await entities.ListAsync(query, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ExternalEntityDetail>>> GetAsync(Guid actorId, Guid entityId, CancellationToken cancellationToken)
    {
        Versioned<ExternalEntityDetail>? entity = await entities.FindDetailAsync(entityId, cancellationToken).ConfigureAwait(false);
        return entity is null ? AdministrationError.NotFound
            : await access.CheckRecordAsync(actorId, PermissionCatalogue.OrganizationView, SubjectOf(entityId), cancellationToken).ConfigureAwait(false) is { } denied ? denied
            : entity;
    }

    public async Task<AdministrationResult<Versioned<ExternalEntityDetail>>> CreateAsync(Guid actorId, ExternalEntityDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (await access.CheckNewRecordAsync(actorId, PermissionCatalogue.OrganizationManage, new(), cancellationToken).ConfigureAwait(false) is { } denied)
        {
            return denied;
        }

        if (await ReferenceIssuesAsync(draft.EntityTypeItemId, draft.SponsorUserId, cancellationToken).ConfigureAwait(false) is { Length: > 0 } issues)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, issues);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ExternalEntity entity = new()
        {
            Id = Guid.CreateVersion7(now),
            Code = draft.Code,
            Name = draft.Name,
            EntityTypeItemId = draft.EntityTypeItemId,
            SponsorUserId = draft.SponsorUserId,
            Status = ExternalEntityStatus.Active,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        entities.Add(entity);
        return await SaveAsync(actorId, entity, "created", cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ExternalEntityDetail>>> UpdateAsync(
        Guid actorId, Guid entityId, ExternalEntityChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        ExternalEntity? entity = await entities.FindForUpdateAsync(entityId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (await CheckWritableAsync(actorId, entity, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (await ReferenceIssuesAsync(changes.EntityTypeItemId, changes.SponsorUserId, cancellationToken).ConfigureAwait(false) is { Length: > 0 } issues)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, issues);
        }

        entity!.Name = changes.Name;
        entity.EntityTypeItemId = changes.EntityTypeItemId;
        entity.SponsorUserId = changes.SponsorUserId;
        return await SaveAsync(actorId, entity, "updated", cancellationToken).ConfigureAwait(false);
    }

    public Task<AdministrationResult<Versioned<ExternalEntityDetail>>> SuspendAsync(Guid actorId, Guid entityId, uint? expectedVersion, CancellationToken cancellationToken) =>
        TransitionAsync(actorId, entityId, expectedVersion, ExternalEntityStatus.Active, ExternalEntityStatus.Suspended, "suspended", cancellationToken);

    public Task<AdministrationResult<Versioned<ExternalEntityDetail>>> ActivateAsync(Guid actorId, Guid entityId, uint? expectedVersion, CancellationToken cancellationToken) =>
        TransitionAsync(actorId, entityId, expectedVersion, ExternalEntityStatus.Suspended, ExternalEntityStatus.Active, "activated", cancellationToken);

    public Task<AdministrationResult<Versioned<ExternalEntityDetail>>> RetireAsync(Guid actorId, Guid entityId, uint? expectedVersion, CancellationToken cancellationToken) =>
        TransitionAsync(actorId, entityId, expectedVersion, from: null, ExternalEntityStatus.Retired, "retired", cancellationToken);

    /// <summary><paramref name="from"/> null: from any status but RETIRED.</summary>
    private async Task<AdministrationResult<Versioned<ExternalEntityDetail>>> TransitionAsync(
        Guid actorId, Guid entityId, uint? expectedVersion, ExternalEntityStatus? from, ExternalEntityStatus to, string change, CancellationToken cancellationToken)
    {
        ExternalEntity? entity = await entities.FindForUpdateAsync(entityId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (await CheckWritableAsync(actorId, entity, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (from is { } required && entity!.Status != required)
        {
            return AdministrationError.InvalidTransition;
        }

        entity!.Status = to;
        return await SaveAsync(actorId, entity, change, cancellationToken).ConfigureAwait(false);
    }

    private static AuthorizationSubject SubjectOf(Guid entityId) => new() { ExternalEntityId = entityId };

    /// <summary>Found, within the caller's ORGANIZATION_MANAGE scope, and not RETIRED.</summary>
    private async Task<AdministrationError?> CheckWritableAsync(Guid actorId, ExternalEntity? entity, CancellationToken cancellationToken) =>
        entity is null ? AdministrationError.NotFound
        : await access.CheckRecordAsync(actorId, PermissionCatalogue.OrganizationManage, SubjectOf(entity.Id), cancellationToken).ConfigureAwait(false) is { } denied ? denied
        : entity.Status == ExternalEntityStatus.Retired ? AdministrationError.TerminalState
        : null;

    private async Task<FieldIssue[]> ReferenceIssuesAsync(Guid entityTypeItemId, Guid? sponsorUserId, CancellationToken cancellationToken)
    {
        FieldIssue?[] issues =
        [
            await entities.IsEntityTypeAsync(entityTypeItemId, cancellationToken).ConfigureAwait(false) ? null : new FieldIssue("entityTypeItemId", FieldIssue.NotFound),
            await ReferenceChecks.ActiveInternalUserAsync(users, sponsorUserId, "sponsorUserId", cancellationToken).ConfigureAwait(false),
        ];
        return [.. issues.OfType<FieldIssue>()];
    }

    private async Task<AdministrationResult<Versioned<ExternalEntityDetail>>> SaveAsync(Guid actorId, ExternalEntity entity, string change, CancellationToken cancellationToken)
    {
        entity.UpdatedAt = timeProvider.GetUtcNow();
        entity.UpdatedBy = actorId;
        if ((await entities.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogEntityChanged(logger, actorId, change, entity.Id);
        return await entities.FindDetailAsync(entity.Id, cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException($"Entity {entity.Id} was saved and cannot be read back.");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Organization administration: {ActorId} {Change} external entity {EntityId}.")]
    private static partial void LogEntityChanged(ILogger logger, Guid actorId, string change, Guid entityId);
}
