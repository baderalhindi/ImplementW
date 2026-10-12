namespace PMPlatform.Application.Features.AuditActivity.Contracts.Events;

public static class AuditActivityAuditAttributes
{
    public const string Category = "category";

    public const string RequestContextRevealed = "request_context_revealed";

    public const string EventsVerified = "events_verified";

    public const string BrokenLinks = "broken_links";

    public const string AttributeDigestMismatches = "attribute_digest_mismatches";

    public const string EventsCompared = "events_compared";

    public const string Missing = "missing";

    public const string Unexpected = "unexpected";

    public const string Changed = "changed";
}
