namespace PMPlatform.Application.Common.Events;

/// <summary>An envelope's <c>messageKey</c> (event-conventions EV-4): <c>&lt;eventType&gt;:&lt;idempotencyKey&gt;</c>.</summary>
public static class EventMessageKey
{
    public static string Of(string eventType, string idempotencyKey) => $"{eventType}:{idempotencyKey}";

    /// <summary>The <c>eventType</c> a message key begins with.</summary>
    public static string EventTypeOf(string messageKey)
    {
        ArgumentNullException.ThrowIfNull(messageKey);
        int separator = messageKey.IndexOf(':', StringComparison.Ordinal);
        return separator > 0 ? messageKey[..separator] : throw new FormatException($"'{messageKey}' is not <eventType>:<idempotencyKey>.");
    }
}
