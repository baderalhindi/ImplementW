using PMPlatform.Application.Common.Correlation;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Application.Features.Project.Contracts.Events;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Project.Import;

/// <summary>
/// The notifications of project import (the 2 Nov 2026 review): each a NOTIFICATION_INTENT that WF-15 routes by its family's roles (ADR-004).
/// One addressed to a person names them in <see cref="NotificationParameterKeys.RecipientUserId"/>, so it reaches them alone.
/// </summary>
internal static class ImportNotifications
{
    public static ProjectImportValidated Validated(
        Guid validationId, Guid importerId, Guid? departmentId, string deepLink, IReadOnlyList<NotificationParameter> parameters, DateTimeOffset now)
    {
        string key = KeyOf(validationId, ProjectImportValidated.Type, now);
        return new ProjectImportValidated
        {
            EventId = Guid.CreateVersion7(now),
            EventType = ProjectImportValidated.Type,
            SchemaVersion = ProjectImportValidated.CurrentSchemaVersion,
            Kind = EventKind.NotificationIntent,
            MessageKey = EventMessageKey.Of(ProjectImportValidated.Type, key),
            IdempotencyKey = key,
            OccurredAt = now,
            SourceModule = Module,
            CorrelationId = CorrelationScope.Current ?? Guid.CreateVersion7(now),
            Actor = new EventActor(AuditActorType.User, importerId),
            Subject = new EventSubject(Module, "ProjectImportValidation", validationId, null),
            Scope = new EventScope(null, departmentId, null),
            Data = DataOf(ProjectImportValidated.EventFamilyCode, key, deepLink, parameters, importerId),
        };
    }

    public static ProjectImportValueRequested ValueRequested(
        Guid requestId, Guid requesterId, string deepLink, IReadOnlyList<NotificationParameter> parameters, DateTimeOffset now)
    {
        string key = KeyOf(requestId, ProjectImportValueRequested.Type, now);
        return new ProjectImportValueRequested
        {
            EventId = Guid.CreateVersion7(now),
            EventType = ProjectImportValueRequested.Type,
            SchemaVersion = ProjectImportValueRequested.CurrentSchemaVersion,
            Kind = EventKind.NotificationIntent,
            MessageKey = EventMessageKey.Of(ProjectImportValueRequested.Type, key),
            IdempotencyKey = key,
            OccurredAt = now,
            SourceModule = Module,
            CorrelationId = CorrelationScope.Current ?? Guid.CreateVersion7(now),
            Actor = new EventActor(AuditActorType.User, requesterId),
            Subject = new EventSubject(Module, "ProjectImportValueRequest", requestId, null),
            Scope = new EventScope(null, null, null),
            Data = DataOf(ProjectImportValueRequested.EventFamilyCode, key, deepLink, parameters, recipientUserId: null),
        };
    }

    public static ProjectImportValueResolved ValueResolved(
        Guid requestId, Guid resolverId, Guid requesterId, Guid? departmentId, string deepLink, IReadOnlyList<NotificationParameter> parameters,
        DateTimeOffset now)
    {
        string key = KeyOf(requestId, ProjectImportValueResolved.Type, now);
        return new ProjectImportValueResolved
        {
            EventId = Guid.CreateVersion7(now),
            EventType = ProjectImportValueResolved.Type,
            SchemaVersion = ProjectImportValueResolved.CurrentSchemaVersion,
            Kind = EventKind.NotificationIntent,
            MessageKey = EventMessageKey.Of(ProjectImportValueResolved.Type, key),
            IdempotencyKey = key,
            OccurredAt = now,
            SourceModule = Module,
            CorrelationId = CorrelationScope.Current ?? Guid.CreateVersion7(now),
            Actor = new EventActor(AuditActorType.User, resolverId),
            Subject = new EventSubject(Module, "ProjectImportValueRequest", requestId, null),
            Scope = new EventScope(null, departmentId, null),
            Data = DataOf(ProjectImportValueResolved.EventFamilyCode, key, deepLink, parameters, requesterId),
        };
    }

    private const string Module = "Project";

    /// <summary>One intent per subject and occurrence; the idempotency key differs from the correlation id (R-45).</summary>
    private static string KeyOf(Guid subjectId, string eventType, DateTimeOffset now) => $"{subjectId:D}:{eventType}:{now.ToUnixTimeMilliseconds()}";

    private static NotificationIntentData DataOf(
        string familyCode, string key, string deepLink, IReadOnlyList<NotificationParameter> parameters, Guid? recipientUserId) =>
        new(
            familyCode,
            key,
            null,
            deepLink,
            recipientUserId is { } recipient
                ? [.. parameters, new NotificationParameter(NotificationParameterKeys.RecipientUserId, recipient.ToString("D"))]
                : parameters,
            null);
}
