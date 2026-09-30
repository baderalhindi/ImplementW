using System.Text.Json;
using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications;

/// <summary>
/// Receives every NOTIFICATION_INTENT after its producer committed (commit-before-notify, Blueprint Section 15; EV-6). It
/// only records the intent: RECEIVED to be routed now, or SCHEDULED for a reminder. Routing and sending happen later, in
/// their own transactions, so nothing that goes wrong there can undo the receipt, and nothing here can reach the
/// producer's transaction, which committed before the dispatcher began. A second delivery of the same message finds the
/// intent already recorded (unique source event type and reference, EV-4) and records nothing.
/// </summary>
/// <remarks>
/// A payload that breaks EV-10 is refused with an exception: the dispatcher keeps the message, retries it and finally
/// leaves it with its error in the outbox (EV-6's dead letter), where it is visible and was never half-recorded.
/// </remarks>
internal sealed partial class NotificationIntentIntake(
    INotificationRepository repository, TimeProvider timeProvider, ILogger<NotificationIntentIntake> logger) : INotificationIntentConsumer
{
    public const int DeepLinkLength = 500;
    public const int ParameterNameLength = 100;
    public const int ParameterValueLength = 2000;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        NotificationIntentEnvelope envelope = EventSerialization.Deserialize<NotificationIntentEnvelope>(payload);
        Check(envelope);
        NotificationIntentData data = envelope.Data;

        if (await repository.IntentExistsAsync(envelope.EventType, data.SourceReference, cancellationToken).ConfigureAwait(false))
        {
            LogDuplicate(logger, envelope.MessageKey);
            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        NotificationIntent intent = new()
        {
            Id = Guid.CreateVersion7(now),
            SourceModule = envelope.SourceModule,
            SourceEventType = envelope.EventType,
            SourceReference = data.SourceReference,
            EventFamilyCode = data.EventFamilyCode,
            SubjectType = envelope.Subject.Type,
            SubjectId = envelope.Subject.Id,
            ScopeProjectId = envelope.Scope.ProjectId,
            OccurredAt = envelope.OccurredAt,
            ReceivedAt = now,
            ScheduledFor = data.ScheduledFor,
            Status = data.ScheduledFor > now ? NotificationIntentStatus.Scheduled : NotificationIntentStatus.Received,
            DeepLink = data.DeepLink,
            CreatedAt = now,
            CreatedBy = NotificationServicePrincipal.Id,
            UpdatedAt = now,
            UpdatedBy = NotificationServicePrincipal.Id,
        };
        repository.Add(intent);
        foreach (NotificationParameter parameter in data.Parameters)
        {
            repository.Add(new NotificationIntentParameter
            {
                Id = Guid.CreateVersion7(now),
                NotificationIntentId = intent.Id,
                ParameterKey = parameter.Name,
                ParameterValue = parameter.Value,
                CreatedAt = now,
                CreatedBy = NotificationServicePrincipal.Id,
                UpdatedAt = now,
                UpdatedBy = NotificationServicePrincipal.Id,
            });
        }

        // The dispatcher's transaction commits this with the message's dispatch mark. A concurrent receipt of the same
        // intent loses on the unique key; the exception rolls this attempt back and the retry finds the intent recorded.
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != NotificationSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"Intent {envelope.MessageKey} was recorded by another dispatch.");
        }
    }

    /// <summary>EV-10 and the envelope rules the receipt relies on.</summary>
    private static void Check(NotificationIntentEnvelope envelope)
    {
        NotificationIntentData? data = envelope.Data;
        List<string> faults = [];
        if (envelope.Kind != EventKind.NotificationIntent)
        {
            faults.Add("kind");
        }

        if (data is null)
        {
            throw new JsonException("The intent has no data.");
        }

        if (string.IsNullOrWhiteSpace(data.EventFamilyCode))
        {
            faults.Add("eventFamilyCode");
        }

        // The envelope is read back later by this key (INotificationRepository.FindEnvelopeAsync).
        if (data.SourceReference != envelope.IdempotencyKey || string.IsNullOrWhiteSpace(data.SourceReference)
            || envelope.MessageKey != EventMessageKey.Of(envelope.EventType, envelope.IdempotencyKey))
        {
            faults.Add("sourceReference");
        }

        // An SPA route: one leading slash, no scheme, no host, no query string that could carry a token.
        if (data.DeepLink is not { Length: > 1 and <= DeepLinkLength } link || link[0] != '/' || link.StartsWith("//", StringComparison.Ordinal)
            || link.Contains("://", StringComparison.Ordinal) || link.Contains('?', StringComparison.Ordinal) || link.Contains('\\', StringComparison.Ordinal))
        {
            faults.Add("deepLink");
        }

        if (data.Parameters is null
            || data.Parameters.Any(p => p is null || !IsParameterName(p.Name) || p.Value is null || p.Value.Length > ParameterValueLength)
            || data.Parameters.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != data.Parameters.Count
            || data.Parameters.Any(p => p.Name == TemplateText.DeepLink))
        {
            faults.Add("parameters");
        }

        if (data.ScheduledFor is not null && data.Condition is null)
        {
            faults.Add("condition");
        }

        if (data.Condition is { } condition
            && (string.IsNullOrWhiteSpace(condition.SubjectType) || condition.SubjectId == Guid.Empty || condition.SatisfiedWhenStatusIn is not { Count: > 0 }))
        {
            faults.Add("condition");
        }

        if (faults.Count > 0)
        {
            throw new JsonException($"Intent {envelope.MessageKey} breaks EV-10: {string.Join(", ", faults.Distinct(StringComparer.Ordinal))}.");
        }
    }

    private static bool IsParameterName(string? name) =>
        name is { Length: > 0 and <= ParameterNameLength } && char.IsAsciiLetter(name[0]) && name.All(char.IsAsciiLetterOrDigit);

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification intent {MessageKey} was already received; the repeated delivery is ignored.")]
    private static partial void LogDuplicate(ILogger logger, string messageKey);
}
