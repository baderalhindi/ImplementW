using PMPlatform.Application.Common.Correlation;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Application.Features.Project.Contracts.Events;
using PMPlatform.Domain.Common;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project;

/// <summary>Review round 7, item 2: the NOTIFICATION_INTENT that tells a Project Manager a project was handed to them, or taken from them.</summary>
internal static class ProjectManagerNotifications
{
    private const string Module = "Project";

    /// <summary>One intent to <paramref name="recipientId"/>; <paramref name="role"/> says which side of the change they are (<c>INCOMING</c>, <c>OUTGOING</c>).</summary>
    public static ProjectManagerChanged Changed(ProjectEntity project, Guid actorId, Guid recipientId, string role, DateTimeOffset now)
    {
        string key = $"{project.Id:D}:{ProjectManagerChanged.Type}:{role}:{now.ToUnixTimeMilliseconds()}";
        return new ProjectManagerChanged
        {
            EventId = Guid.CreateVersion7(now),
            EventType = ProjectManagerChanged.Type,
            SchemaVersion = ProjectManagerChanged.CurrentSchemaVersion,
            Kind = EventKind.NotificationIntent,
            MessageKey = EventMessageKey.Of(ProjectManagerChanged.Type, key),
            IdempotencyKey = key,
            OccurredAt = now,
            SourceModule = Module,
            CorrelationId = CorrelationScope.Current ?? Guid.CreateVersion7(now),
            Actor = new EventActor(AuditActorType.User, actorId),
            Subject = new EventSubject(Module, "Project", project.Id, project.RevisionNo),
            Scope = new EventScope(project.Id, project.DepartmentId, project.ExternalEntityId),
            Data = new NotificationIntentData(
                ProjectManagerChanged.EventFamilyCode,
                key,
                null,
                $"/projects/{project.Id:D}",
                [
                    new NotificationParameter("projectTitle", project.Title.Text),
                    new NotificationParameter("formalProjectId", project.FormalProjectId ?? string.Empty),
                    new NotificationParameter("change", role),
                    new NotificationParameter(NotificationParameterKeys.RecipientUserId, recipientId.ToString("D")),
                ],
                null),
        };
    }
}
