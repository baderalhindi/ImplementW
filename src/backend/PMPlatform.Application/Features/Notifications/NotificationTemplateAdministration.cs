using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Governance;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Application.Features.Notifications.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications;

/// <summary>
/// Notification templates under the governed lifecycle primitive (ERD D-12): only the author edits a DRAFT, a second
/// person validates, a third publishes. The content rules are checked on every draft change and again at validation and
/// publication. Publishing retires the PUBLISHED version of the same event type and channel in the same save, so
/// routing finds one.
/// </summary>
internal sealed partial class NotificationTemplateAdministration(
    INotificationRepository repository,
    NotificationDeliveryPolicy policy,
    IAuditTrail audit,
    TimeProvider timeProvider,
    ILogger<NotificationTemplateAdministration> logger) : INotificationTemplateAdministration
{
    public async Task<NotificationTemplatePage> ListAsync(NotificationTemplateQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        (IReadOnlyList<NotificationTemplate> items, int total) = await repository.ListTemplatesAsync(query, cancellationToken).ConfigureAwait(false);
        return new NotificationTemplatePage([.. items.Select(NotificationMapping.ToSummary)], query.Page.Page, query.Page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<NotificationTemplateDetail>>> GetAsync(Guid templateId, CancellationToken cancellationToken) =>
        await repository.FindTemplateAsync(templateId, null, cancellationToken).ConfigureAwait(false) is { } template
            ? new Versioned<NotificationTemplateDetail>(NotificationMapping.ToDetail(template), repository.RowVersionOf(template))
            : AdministrationError.NotFound;

    public async Task<AdministrationResult<Versioned<NotificationTemplateDetail>>> CreateAsync(Guid actorId, NotificationTemplateDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (NotificationTemplateRules.Check(draft.Channel, draft.SubjectAr, draft.SubjectEn, draft.BodyAr, draft.BodyEn, policy) is { Count: > 0 } issues)
        {
            return AdministrationError.Rule(NotificationErrorCodes.TemplateInvalid, [.. issues]);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        NotificationTemplate template = new()
        {
            Id = Guid.CreateVersion7(now),
            EventFamilyCode = draft.EventFamilyCode,
            EventType = draft.EventType,
            Channel = draft.Channel,
            VersionNo = await repository.GetLatestTemplateVersionNoAsync(draft.EventType, draft.Channel, cancellationToken).ConfigureAwait(false) + 1,
            SubjectAr = draft.SubjectAr,
            SubjectEn = draft.SubjectEn,
            BodyAr = draft.BodyAr,
            BodyEn = draft.BodyEn,
            LifecycleState = GovernedLifecycleState.Draft,
            CreatedAt = now,
            CreatedBy = actorId,
        };
        repository.Add(template);
        return await SaveAsync(actorId, template, "created", NotificationAuditEvents.TemplateCreated,
            [
                AuditAttribute.Change("event_family_code", null, template.EventFamilyCode),
                AuditAttribute.Change("event_type", null, template.EventType),
                AuditAttribute.Change("channel", null, template.Channel),
                AuditAttribute.Change("version_no", null, template.VersionNo),
                AuditAttribute.Change("subject_ar", null, template.SubjectAr),
                AuditAttribute.Change("subject_en", null, template.SubjectEn),
                AuditAttribute.Change("body_ar", null, template.BodyAr),
                AuditAttribute.Change("body_en", null, template.BodyEn),
                AuditAttribute.Change("lifecycle_state", null, template.LifecycleState),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<NotificationTemplateDetail>>> UpdateAsync(
        Guid actorId, Guid templateId, NotificationTemplateChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        NotificationTemplate? template = await repository.FindTemplateAsync(templateId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return AdministrationError.NotFound;
        }

        if (NotificationRefusal.Of(GovernedLifecycle.CheckDraftEdit(template, actorId)) is { } refused)
        {
            return refused;
        }

        if (NotificationTemplateRules.Check(template.Channel, changes.SubjectAr, changes.SubjectEn, changes.BodyAr, changes.BodyEn, policy) is { Count: > 0 } issues)
        {
            return AdministrationError.Rule(NotificationErrorCodes.TemplateInvalid, [.. issues]);
        }

        AuditAttribute?[] changed =
        [
            AuditAttribute.Change("subject_ar", template.SubjectAr, changes.SubjectAr),
            AuditAttribute.Change("subject_en", template.SubjectEn, changes.SubjectEn),
            AuditAttribute.Change("body_ar", template.BodyAr, changes.BodyAr),
            AuditAttribute.Change("body_en", template.BodyEn, changes.BodyEn),
        ];
        template.SubjectAr = changes.SubjectAr;
        template.SubjectEn = changes.SubjectEn;
        template.BodyAr = changes.BodyAr;
        template.BodyEn = changes.BodyEn;
        return await SaveAsync(actorId, template, "updated", NotificationAuditEvents.TemplateUpdated, changed, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<NotificationTemplateDetail>>> ValidateAsync(
        Guid actorId, Guid templateId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        NotificationTemplate? template = await repository.FindTemplateAsync(templateId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return AdministrationError.NotFound;
        }

        if (NotificationRefusal.Of(GovernedLifecycle.CheckValidate(template, actorId)) is { } refused)
        {
            return refused;
        }

        if (RulesOf(template) is { } invalid)
        {
            return invalid;
        }

        GovernedLifecycle.Validate(template, actorId, timeProvider.GetUtcNow());
        return await SaveAsync(actorId, template, "validated", NotificationAuditEvents.TemplateValidated,
            [AuditAttribute.Change("lifecycle_state", GovernedLifecycleState.Draft, template.LifecycleState)], cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<NotificationTemplateDetail>>> PublishAsync(
        Guid actorId, Guid templateId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        NotificationTemplate? template = await repository.FindTemplateAsync(templateId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return AdministrationError.NotFound;
        }

        if (NotificationRefusal.Of(GovernedLifecycle.CheckPublish(template, actorId)) is { } refused)
        {
            return refused;
        }

        if (RulesOf(template) is { } invalid)
        {
            return invalid;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        IReadOnlyList<NotificationTemplate> superseded = await repository.GetPublishedTemplatesAsync(template.EventType, template.Channel, template.Id, cancellationToken).ConfigureAwait(false);
        foreach (NotificationTemplate previous in superseded)
        {
            GovernedLifecycle.Retire(previous, now);
            previous.UpdatedAt = now;
            previous.UpdatedBy = actorId;
            audit.Stage(NotificationAudit.Template(NotificationAuditEvents.TemplateRetired, actorId, previous,
                [
                    AuditAttribute.Change("lifecycle_state", GovernedLifecycleState.Published, previous.LifecycleState),
                    AuditAttribute.Of("superseded_by_template_id", template.Id),
                ]));
        }

        GovernedLifecycle.Publish(template, actorId, now);
        return await SaveAsync(actorId, template, "published", NotificationAuditEvents.TemplatePublished,
            [
                AuditAttribute.Change("lifecycle_state", GovernedLifecycleState.Validated, template.LifecycleState),
                .. superseded.Select(previous => AuditAttribute.Of("supersedes_template_id", previous.Id)),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<NotificationTemplateDetail>>> RetireAsync(
        Guid actorId, Guid templateId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        NotificationTemplate? template = await repository.FindTemplateAsync(templateId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return AdministrationError.NotFound;
        }

        GovernedLifecycleState before = template.LifecycleState;
        return NotificationRefusal.Of(GovernedLifecycle.Retire(template, timeProvider.GetUtcNow())) is { } refused
            ? refused
            : await SaveAsync(actorId, template, "retired", NotificationAuditEvents.TemplateRetired,
                [AuditAttribute.Change("lifecycle_state", before, template.LifecycleState)], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A template authored under an earlier budget is checked again before it can be validated or published.</summary>
    private AdministrationError? RulesOf(NotificationTemplate template) =>
        NotificationTemplateRules.Check(template.Channel, template.SubjectAr, template.SubjectEn, template.BodyAr, template.BodyEn, policy) is { Count: > 0 } issues
            ? AdministrationError.Rule(NotificationErrorCodes.TemplateInvalid, [.. issues])
            : null;

    private async Task<AdministrationResult<Versioned<NotificationTemplateDetail>>> SaveAsync(
        Guid actorId, NotificationTemplate template, string change, string eventType, AuditAttribute?[] attributes, CancellationToken cancellationToken)
    {
        template.UpdatedAt = timeProvider.GetUtcNow();
        template.UpdatedBy = actorId;
        audit.Stage(NotificationAudit.Template(eventType, actorId, template, attributes));
        if (NotificationRefusal.Of(await repository.SaveAsync(cancellationToken).ConfigureAwait(false)) is { } saveError)
        {
            return saveError;
        }

        LogChanged(logger, actorId, change, template.Id);
        NotificationTemplate saved = await repository.FindTemplateAsync(template.Id, null, cancellationToken).ConfigureAwait(false)
                                     ?? throw new InvalidOperationException($"Notification template {template.Id} was saved and cannot be read back.");
        return new Versioned<NotificationTemplateDetail>(NotificationMapping.ToDetail(saved), repository.RowVersionOf(saved));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification template administration: {ActorId} {Change} template {TemplateId}.")]
    private static partial void LogChanged(ILogger logger, Guid actorId, string change, Guid templateId);
}
