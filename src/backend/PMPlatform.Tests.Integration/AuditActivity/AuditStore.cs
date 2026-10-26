using PMPlatform.Tests.Integration.Persistence;

namespace PMPlatform.Tests.Integration.AuditActivity;

/// <summary>Reads <c>audit_activity</c> as a test checks it: the events of one request, found by its <c>X-Correlation-Id</c>.</summary>
internal static class AuditStore
{
    /// <summary>
    /// The events of the request, oldest first, as <c>event_class|event_type|outcome|actor_user_id|subject_id</c>, followed by
    /// its attributes in brackets as <c>name=old&gt;new</c>, sorted, without the client address.
    /// </summary>
    public static Task<IReadOnlyList<string>> EventsAsync(this TestDatabase database, Guid correlationId) => database.QueryAsync($"""
        SELECT concat_ws('|', e.event_class, e.event_type, e.outcome, coalesce(e.actor_user_id::text, ''), coalesce(e.subject_id::text, ''))
               || ' [' || coalesce((SELECT string_agg(a.attribute_name || '=' || coalesce(a.old_value, '') || '>' || coalesce(a.new_value, ''), ', ' ORDER BY a.attribute_name)
                                     FROM audit_activity.audit_event_attribute a
                                     WHERE a.audit_event_id = e.id AND a.attribute_name <> 'client_address'), '') || ']'
        FROM audit_activity.audit_event e
        WHERE e.correlation_id = '{correlationId}'
        ORDER BY e.recorded_at
        """);

    /// <summary>Events whose hash is not the hash of their row, or whose previous hash is not their predecessor's: none if the chain is intact.</summary>
    public const string BrokenChainLinks = """
        SELECT c.id::text
        FROM (SELECT e.id, e.event_hash, e.previous_event_hash, audit_activity.audit_event_hash(e) AS recomputed,
                     lag(e.event_hash) OVER (ORDER BY e.recorded_at, e.id) AS predecessor
              FROM audit_activity.audit_event e) c
        WHERE c.event_hash <> c.recomputed OR c.previous_event_hash IS DISTINCT FROM c.predecessor
        """;

    /// <summary>A client whose every request carries <paramref name="correlationId"/>, so its audit events can be found.</summary>
    public static HttpClient WithCorrelationId(this HttpClient client, Guid correlationId)
    {
        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId.ToString());
        return client;
    }
}
