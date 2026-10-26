using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Notifications;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>What the two idle workers would do, on demand, and the rows a test reads back.</summary>
internal static class NotificationDriver
{
    /// <summary>The outbox dispatcher's pass: every due message to its consumer.</summary>
    public static Task<int> DispatchAsync(this IdentityApiFactory api) =>
        api.Services.GetRequiredService<IOutboxDispatcher>().DispatchDueAsync(500, CancellationToken.None);

    /// <summary>The notification worker's pass, in its own scope as the worker runs it.</summary>
    public static async Task<int> ProcessAsync(this IdentityApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<INotificationProcessing>().RunAsync(500, CancellationToken.None);
    }

    /// <summary>Dispatch, then process: a committed intent received, routed and, where a channel answers, sent.</summary>
    public static async Task DeliverAsync(this IdentityApiFactory api)
    {
        await api.DispatchAsync();
        await api.ProcessAsync();
    }

    public static string IntentOf(string eventType, Guid reference) =>
        $"SELECT status || '|' || coalesce(suppression_reason, '') FROM notifications.notification_intent WHERE source_event_type = '{eventType}' AND source_reference = '{reference}'";

    /// <summary>The intent's deliveries as "recipient n|channel|status|reason", sorted.</summary>
    public static string DeliveriesOf(string eventType, Guid reference) => $"""
        SELECT right(d.recipient_user_id::text, 1) || '|' || d.channel || '|' || d.status || '|' || coalesce(d.suppression_reason, '')
        FROM notifications.notification_delivery d JOIN notifications.notification_intent i ON i.id = d.notification_intent_id
        WHERE i.source_event_type = '{eventType}' AND i.source_reference = '{reference}'
        ORDER BY 1
        """;
}
