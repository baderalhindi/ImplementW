namespace PMPlatform.Application.Features.AuditActivity.Contracts;

/// <summary>
/// FG-06 §22 codes AuditActivity uses: why a captured event is refused (recorded as the outbox message's last error, never
/// returned to a caller), and the refusals of its own API.
/// </summary>
public static class AuditActivityErrorCodes
{
    public const string SchemaUnsupported = "AUDIT_SCHEMA_UNSUPPORTED";

    public const string EventInvalid = "AUDIT_EVENT_INVALID";

    public const string EventTypeUnsupported = "AUDIT_EVENT_TYPE_UNSUPPORTED";

    public const string ActorInvalid = "AUDIT_ACTOR_INVALID";

    public const string SourceReferenceInvalid = "AUDIT_SOURCE_REFERENCE_INVALID";

    public const string ChangeSetInvalid = "AUDIT_CHANGESET_INVALID";

    /// <summary>The payload of an AUDIT_EVENT message could not be read as a capture.</summary>
    public const string IngestionFailed = "AUDIT_INGESTION_FAILED";
}
