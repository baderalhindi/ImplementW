using PMPlatform.Application.Common.Auditing;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// Captures an <see cref="AuditEntry"/> as an <see cref="AuditCapture"/> (TASK-033, TASK-073): the request's correlation id and
/// client address are added, secrets are redacted, and the capture is validated before it is written, so a malformed event fails
/// its producer's action instead of reaching the store. Staged captures commit with the producer's change through the outbox
/// (M-6, BR-AUD-010); every class is captured this way, so an event that cannot be captured blocks its action (TBC-AUD-03).
/// </summary>
internal sealed class AuditTrail(IAuditCaptureStore store, IAuditRequestContext request, TimeProvider timeProvider) : IAuditTrail
{
    /// <summary>The author of an event whose actor is not known (<see cref="AuditCapturePrincipal"/>).</summary>
    public static readonly Guid AuditCapturePrincipalId = AuditCapturePrincipal.Id;

    internal const string ClientAddressAttribute = "client_address";

    public void Stage(AuditEntry entry) => store.Stage(Capture(entry));

    public Task RecordAsync(AuditEntry entry) => store.RecordAsync(Capture(entry));

    private AuditCapture Capture(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        DateTimeOffset now = timeProvider.GetUtcNow();
        IEnumerable<AuditAttribute> attributes = request.ClientAddress is { } address
            ? entry.Attributes.Append(AuditAttribute.Of(ClientAddressAttribute, address))
            : entry.Attributes;

        AuditCapture capture = new(
            Guid.CreateVersion7(now),
            AuditCapture.CurrentSchemaVersion,
            now,
            entry.EventClass,
            entry.EventType,
            entry.Outcome,
            entry.ActorType,
            entry.ActorUserId,
            entry.Subject,
            entry.ScopeProjectId,
            entry.ScopeExternalEntityId,
            entry.DataClassificationItemId,
            request.CorrelationId,
            AuditCaptureRules.Redact(attributes));

        IReadOnlyList<string> problems = AuditCaptureRules.Problems(capture);
        return problems.Count == 0
            ? capture
            : throw new InvalidOperationException($"Audit event {entry.EventType} cannot be captured: {string.Join(", ", problems)}.");
    }
}
