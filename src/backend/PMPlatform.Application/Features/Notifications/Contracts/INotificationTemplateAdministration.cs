using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary>
/// The bilingual, mandatory notification templates (ADR-012, ADR-004) under the governed lifecycle: an author drafts, a
/// second person validates, a third publishes. Publishing a version retires the one it replaces, so exactly one version
/// of an event type and channel is rendered. A PUBLISHED template never changes; a new wording is a new version.
/// </summary>
public interface INotificationTemplateAdministration
{
    public Task<NotificationTemplatePage> ListAsync(NotificationTemplateQuery query, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<NotificationTemplateDetail>>> GetAsync(Guid templateId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<NotificationTemplateDetail>>> CreateAsync(Guid actorId, NotificationTemplateDraft draft, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<NotificationTemplateDetail>>> UpdateAsync(
        Guid actorId, Guid templateId, NotificationTemplateChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<NotificationTemplateDetail>>> ValidateAsync(Guid actorId, Guid templateId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<NotificationTemplateDetail>>> PublishAsync(Guid actorId, Guid templateId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<NotificationTemplateDetail>>> RetireAsync(Guid actorId, Guid templateId, uint? expectedVersion, CancellationToken cancellationToken);
}
