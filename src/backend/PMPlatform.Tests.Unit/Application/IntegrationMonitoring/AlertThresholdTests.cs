using PMPlatform.Application.Features.IntegrationMonitoring;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Tests.Unit.Application.IntegrationMonitoring;

/// <summary>
/// TASK-092's thresholds (PTBC-037; TBC-INT-08, -11, -15): a backlog is exceeded strictly beyond its depth or its oldest age, an
/// availability alert delay counts from the moment the integration went down, and a policy that names a threshold without a usable bound
/// stops the API rather than alerting on nothing.
/// </summary>
public sealed class AlertThresholdTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ABacklogIsExceededBeyondItsDepthNotAtIt()
    {
        QueueBacklogThreshold threshold = new() { Depth = 3 };

        Assert.False(IntegrationHealth.BacklogExceeds(new IntegrationQueueState(1, 2, T, 0), threshold, T.AddHours(1)));
        Assert.True(IntegrationHealth.BacklogExceeds(new IntegrationQueueState(1, 3, T, 0), threshold, T));
    }

    [Fact]
    public void ABacklogIsExceededWhenItsOldestOperationHasWaitedBeyondItsAge()
    {
        QueueBacklogThreshold threshold = new() { OldestAge = TimeSpan.FromMinutes(10) };
        IntegrationQueueState queue = new(0, 1, T, 0);

        Assert.False(IntegrationHealth.BacklogExceeds(queue, threshold, T.AddMinutes(10)));
        Assert.True(IntegrationHealth.BacklogExceeds(queue, threshold, T.AddMinutes(10).AddTicks(1)));
        Assert.False(IntegrationHealth.BacklogExceeds(IntegrationQueueState.Empty, threshold, T.AddDays(1)));
    }

    [Fact]
    public void EitherBoundAloneExceedsABacklogThatHasBoth()
    {
        QueueBacklogThreshold threshold = new() { Depth = 5, OldestAge = TimeSpan.FromMinutes(10) };

        Assert.True(IntegrationHealth.BacklogExceeds(new IntegrationQueueState(0, 6, T, 0), threshold, T));
        Assert.True(IntegrationHealth.BacklogExceeds(new IntegrationQueueState(0, 1, T, 0), threshold, T.AddHours(1)));
        Assert.False(IntegrationHealth.BacklogExceeds(new IntegrationQueueState(0, 5, T, 0), threshold, T.AddMinutes(10)));
    }

    /// <summary>Dead letters are not queued work: they wait for an operator and have an alert of their own (DEAD_LETTER).</summary>
    [Fact]
    public void DeadLettersAreNotPartOfTheBacklog()
    {
        IntegrationQueueState queue = new(0, 0, null, 40);

        Assert.Equal(0, queue.Depth);
        Assert.False(IntegrationHealth.BacklogExceeds(queue, new QueueBacklogThreshold { Depth = 0 }, T));
    }

    [Fact]
    public void AChangeOfAvailabilityIsDatedByTheAttemptThatMadeIt()
    {
        IntegrationInstance instance = new() { Code = "PRIMARY", EndpointConfigurationKey = "KEY" };

        IntegrationHealth.Apply(instance, Attempt(InvocationStatus.Succeeded, null, T));
        Assert.Equal(T, instance.AvailabilityChangedAt);
        IntegrationHealth.Apply(instance, Attempt(InvocationStatus.RetryScheduled, InvocationFailureCategory.Technical, T.AddMinutes(1)));
        Assert.Equal(T.AddMinutes(1), instance.AvailabilityChangedAt);

        // Still down, and a refusal is the provider answering: neither moves the date.
        IntegrationHealth.Apply(instance, Attempt(InvocationStatus.DeadLetter, InvocationFailureCategory.Technical, T.AddMinutes(2)));
        IntegrationHealth.Apply(instance, Attempt(InvocationStatus.Failed, InvocationFailureCategory.BusinessValidation, T.AddMinutes(3)));
        Assert.Equal(T.AddMinutes(1), instance.AvailabilityChangedAt);
    }

    [Fact]
    public void AnAvailabilityDelayCountsFromWhenTheIntegrationWentDown()
    {
        IntegrationInstance instance = new()
        {
            Code = "PRIMARY",
            EndpointConfigurationKey = "KEY",
            Availability = IntegrationAvailability.Down,
            AvailabilityChangedAt = T,
        };
        TimeSpan delay = TimeSpan.FromMinutes(5);

        Assert.False(IntegrationHealth.DownFor(instance, delay, T.AddMinutes(5).AddTicks(-1)));
        Assert.True(IntegrationHealth.DownFor(instance, delay, T.AddMinutes(5)));

        instance.Availability = IntegrationAvailability.Up;
        Assert.False(IntegrationHealth.DownFor(instance, delay, T.AddHours(1)));
    }

    [Fact]
    public void ABacklogAlertIsAWarning() =>
        Assert.Equal(OperationalAlertSeverity.Warning, OperationalAlerting.SeverityOf(OperationalAlertType.QueueBacklog));

    [Fact]
    public void WithoutAnyThresholdThePolicyIsValidAndInventsNone()
    {
        IntegrationMonitoringPolicy policy = new();

        Assert.True(policy.IsValid());
        Assert.Null(policy.AvailabilityAlertDelayOf("DIRECTORY"));
        Assert.Equal(0, policy.MismatchToleranceOf("DIRECTORY"));
        Assert.Empty(policy.QueueBacklogThresholds);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(-1, null)]
    [InlineData(null, "00:00:00")]
    [InlineData(2, "-00:01:00")]
    public void ABacklogThresholdWithoutAUsableBoundIsRefused(int? depth, string? oldestAge)
    {
        IntegrationMonitoringPolicy policy = new();
        policy.QueueBacklogThresholds["SMS"] = new QueueBacklogThreshold { Depth = depth, OldestAge = oldestAge is null ? null : TimeSpan.Parse(oldestAge, System.Globalization.CultureInfo.InvariantCulture) };

        Assert.False(policy.IsValid());
    }

    [Fact]
    public void AZeroDelayOrANegativeToleranceIsRefused()
    {
        IntegrationMonitoringPolicy delay = new();
        delay.AvailabilityAlertDelays["DIRECTORY"] = TimeSpan.Zero;
        IntegrationMonitoringPolicy tolerance = new();
        tolerance.ReconciliationMismatchTolerances["DIRECTORY"] = -1;
        IntegrationMonitoringPolicy valid = new();
        valid.AvailabilityAlertDelays["DIRECTORY"] = TimeSpan.FromMinutes(5);
        valid.ReconciliationMismatchTolerances["DIRECTORY"] = 0;
        valid.QueueBacklogThresholds["SMS"] = new QueueBacklogThreshold { Depth = 0 };

        Assert.False(delay.IsValid());
        Assert.False(tolerance.IsValid());
        Assert.True(valid.IsValid());
    }

    private static Invocation Attempt(InvocationStatus status, InvocationFailureCategory? category, DateTimeOffset completedAt) =>
        new() { OperationCode = "CALL", Status = status, FailureCategory = category, StartedAt = completedAt, CompletedAt = completedAt };
}
