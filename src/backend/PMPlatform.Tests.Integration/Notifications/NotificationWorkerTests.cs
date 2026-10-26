using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>The two hosted workers, running as they do in the API, take a committed intent to its recipients with no test driving them.</summary>
[Collection(NotificationSuite.Name)]
public sealed class NotificationWorkerTests(NotificationTestHost host)
{
    [Fact]
    public async Task WorkersDeliverWithoutADriver()
    {
        Guid escalation = Guid.NewGuid();
        await using IdentityApiFactory api = host.CreateApi(
            new Dictionary<string, string?> { ["Outbox:PollInterval"] = "00:00:00.200", ["Notifications:Worker:PollInterval"] = "00:00:00.200" },
            withWorkers: true);
        using HttpClient started = api.CreateClient();

        await TestNotificationSource.Escalation(escalation, "ISS-WORKER-1", NotificationTestHost.EntityProjectId, NotificationTestHost.DepartmentId, NotificationTestHost.EntityId, host.Clock.GetUtcNow())
            .RunAsync(api);

        IReadOnlyList<string> status = [];
        for (int i = 0; i < 100 && status is not ["COMPLETED|"]; i++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            status = await host.Database.QueryAsync(NotificationDriver.IntentOf(NotificationTestHost.EscalatedEvent, escalation));
        }

        Assert.Equal(["COMPLETED|"], status);
        Assert.Contains(host.Smtp.To(NotificationTestHost.EmailOf(8)), m => m.Content.Contains("ISS-WORKER-1", StringComparison.Ordinal));
    }
}
