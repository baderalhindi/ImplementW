using PMPlatform.Application.Common.Correlation;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation.MonthlyUpdates;

/// <summary>
/// <c>ExternalParticipation.MonthlyUpdateReminded</c>: the NOTIFICATION_INTENT of a monthly update to its one recipient (the 5 Nov 2026 review §2.1): sent, three days before its due
/// date, on the day, then daily. Past the due date, it also goes to the reviewer once. The family needs a NOTIFICATION_ROUTING entry and a template, which AHDA publishes.
/// </summary>
public sealed record MonthlyUpdateReminded : EventEnvelope<NotificationIntentData>
{
    public const string Type = "ExternalParticipation.MonthlyUpdateReminded";

    public const int CurrentSchemaVersion = 1;

    public const string EventFamilyCode = "MONTHLY_UPDATE_REMINDER";

    /// <summary>What this reminder says: SENT, DUE_SOON, DUE_TODAY, OVERDUE or LATE_TO_REVIEWER.</summary>
    public const string StageParameter = "stage";

    public static MonthlyUpdateReminded Of(ExternalUpdateRequest request, Guid departmentId, Guid recipientId, string stage, Guid actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        string key = $"{request.Id:D}:{recipientId:D}:{stage}:{DateOnly.FromDateTime(now.UtcDateTime):yyyy-MM-dd}";
        return new MonthlyUpdateReminded
        {
            EventId = Guid.CreateVersion7(now),
            EventType = Type,
            SchemaVersion = CurrentSchemaVersion,
            Kind = EventKind.NotificationIntent,
            MessageKey = EventMessageKey.Of(Type, key),
            IdempotencyKey = key,
            OccurredAt = now,
            SourceModule = "ExternalParticipation",
            CorrelationId = CorrelationScope.Current ?? Guid.CreateVersion7(now),
            Actor = new EventActor(AuditActorType.User, actorId),
            Subject = new EventSubject("ExternalParticipation", nameof(ExternalUpdateRequest), request.Id, null),
            Scope = new EventScope(request.ProjectId, departmentId, request.ExternalEntityId),
            Data = new NotificationIntentData(
                EventFamilyCode,
                key,
                null,
                $"/external-requests/{request.Id:D}",
                [
                    new NotificationParameter(StageParameter, stage),
                    new NotificationParameter("dueDate", $"{request.DueDate:yyyy-MM-dd}"),
                    new NotificationParameter(NotificationParameterKeys.RecipientUserId, recipientId.ToString("D")),
                ],
                null),
        };
    }
}
