namespace PMPlatform.Application.Features.Notifications;

/// <summary>One pass of the WF-15 runtime, run by the notification worker: route what is due, send what is due, close what is done.</summary>
public interface INotificationProcessing
{
    /// <summary>Handles up to <paramref name="batchSize"/> of each; returns how many intents and deliveries it moved on.</summary>
    public Task<int> RunAsync(int batchSize, CancellationToken cancellationToken);
}
