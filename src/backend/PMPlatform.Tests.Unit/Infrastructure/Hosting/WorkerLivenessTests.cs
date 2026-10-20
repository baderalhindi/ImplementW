using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PMPlatform.Infrastructure.Hosting;

namespace PMPlatform.Tests.Unit.Infrastructure.Hosting;

/// <summary>
/// TASK-091: a background worker is live unless one pass has outlasted HealthChecks:WorkerPassTimeout, and every pass a
/// <see cref="PollingWorker"/> runs — one that fails included — is recorded while it runs and only then.
/// </summary>
public sealed class WorkerLivenessTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan PassTimeout = TimeSpan.FromHours(2);

    private readonly WorkerActivity _activity = new();

    [Fact]
    public async Task WorkersBetweenPassesAreLive()
    {
        Assert.Equal(HealthStatus.Healthy, (await CheckAsync()).Status);
    }

    [Fact]
    public async Task APassUpToTheTimeoutIsBusyNotStalled()
    {
        _activity.PassStarted("Notification", Now - PassTimeout);
        _activity.PassStarted("Outbox dispatch", Now - TimeSpan.FromSeconds(1));

        Assert.Equal(HealthStatus.Healthy, (await CheckAsync()).Status);
    }

    [Fact]
    public async Task APassLongerThanTheTimeoutIsAStallAndItsWorkerIsNamed()
    {
        _activity.PassStarted("Report jobs", Now - PassTimeout - TimeSpan.FromSeconds(1));
        _activity.PassStarted("Notification", Now - TimeSpan.FromDays(1));
        _activity.PassStarted("Outbox dispatch", Now - TimeSpan.FromSeconds(1));

        HealthCheckResult result = await CheckAsync();

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("In one pass for longer than 02:00:00: Notification, Report jobs.", result.Description);
    }

    [Fact]
    public async Task APassIsRecordedWhileItRunsWhetherItSucceedsOrFails()
    {
        TestWorker worker = new(_activity);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await worker.PassRunning.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal([TestWorker.Name], _activity.PassesInProgress().Keys);

            worker.Finish.SetException(new InvalidOperationException("The pass failed."));
            await WaitUntilAsync(() => _activity.PassesInProgress().Count == 0);
        }
        finally
        {
            // The pass is released first and the stop bounded, so a failed assertion above fails the test instead of hanging it.
            worker.Finish.TrySetResult(0);
            using CancellationTokenSource stopping = new(TimeSpan.FromSeconds(10));
            await worker.StopAsync(stopping.Token);
        }

        Assert.Empty(_activity.PassesInProgress());
    }

    private Task<HealthCheckResult> CheckAsync() =>
        new WorkerLivenessHealthCheck(_activity, Options.Create(new WorkerLivenessOptions { WorkerPassTimeout = PassTimeout }), new FixedClock(Now))
            .CheckHealthAsync(new HealthCheckContext());

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>A worker whose first pass runs until the test finishes it; later passes wait out a long interval.</summary>
    private sealed class TestWorker(WorkerActivity activity)
        : PollingWorker(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), activity, TimeProvider.System, NullLogger.Instance)
    {
        public const string Name = "Test pass";

        public TaskCompletionSource PassRunning { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<int> Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override TimeSpan PollInterval => TimeSpan.FromHours(1);

        protected override int BatchSize => 10;

        protected override string PassName => Name;

        protected override Task<int> RunPassAsync(IServiceProvider services, int batchSize, CancellationToken cancellationToken)
        {
            PassRunning.TrySetResult();
            return Finish.Task;
        }
    }
}
