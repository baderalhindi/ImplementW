using System.Text.Json;
using System.Text.Json.Serialization;

namespace PMPlatform.Application.Common.Events;

/// <summary>
/// How an envelope is written into the outbox and read back by its consumer: camelCase properties, enumerations in
/// upper snake case (event-conventions EV-3, api-conventions R-19), UTC timestamps.
/// </summary>
public static class EventSerialization
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    public static string Serialize<TData>(EventEnvelope<TData> envelope)
        where TData : class
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonSerializer.Serialize(envelope, envelope.GetType(), Options);
    }

    /// <exception cref="JsonException">The payload is not a <typeparamref name="TEvent"/>.</exception>
    public static TEvent Deserialize<TEvent>(string payload)
        where TEvent : class =>
        JsonSerializer.Deserialize<TEvent>(payload, Options) ?? throw new JsonException($"The payload is not a {typeof(TEvent).Name}.");
}
