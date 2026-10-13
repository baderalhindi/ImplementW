using PMPlatform.Application.Features.IntegrationMonitoring;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Tests.Unit.Application.IntegrationMonitoring;

/// <summary>
/// FG-05 §5: availability follows the latest technical outcome, whatever order attempts arrive in, and a business or security refusal says
/// nothing about it; freshness comes from the last success and a configured window, never from availability, and is UNKNOWN without one.
/// </summary>
public sealed class IntegrationHealthTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ASuccessMakesItUpAndALaterTechnicalFailureDown()
    {
        IntegrationInstance instance = Instance();

        Assert.Equal(IntegrationAvailability.Unknown, IntegrationHealth.Apply(instance, Attempt(InvocationStatus.Succeeded, null, T)));
        Assert.Equal(IntegrationAvailability.Up, instance.Availability);
        Assert.Equal(IntegrationAvailability.Up, IntegrationHealth.Apply(instance, Attempt(InvocationStatus.RetryScheduled, InvocationFailureCategory.Technical, T.AddMinutes(1))));
        Assert.Equal(IntegrationAvailability.Down, instance.Availability);
        Assert.Equal((T, T.AddMinutes(1)), (instance.LastSuccessAt, instance.LastFailureAt));
    }

    /// <summary>Telemetry is at least once and unordered (EV-5): an older failure arriving after a newer success leaves it up.</summary>
    [Fact]
    public void TheLatestTechnicalOutcomeWinsWhateverOrderAttemptsArriveIn()
    {
        IntegrationInstance instance = Instance();

        IntegrationHealth.Apply(instance, Attempt(InvocationStatus.Succeeded, null, T.AddMinutes(5)));
        IntegrationHealth.Apply(instance, Attempt(InvocationStatus.Failed, InvocationFailureCategory.Technical, T));

        Assert.Equal(IntegrationAvailability.Up, instance.Availability);
        Assert.Equal(T.AddMinutes(5), instance.LastSuccessAt);
    }

    [Theory]
    [InlineData(InvocationFailureCategory.BusinessValidation)]
    [InlineData(InvocationFailureCategory.Security)]
    public void ARefusalIsTheProviderAnsweringNotAnOutage(InvocationFailureCategory category)
    {
        IntegrationInstance instance = Instance();
        IntegrationHealth.Apply(instance, Attempt(InvocationStatus.Succeeded, null, T));

        IntegrationHealth.Apply(instance, Attempt(InvocationStatus.Failed, category, T.AddMinutes(1)));

        Assert.Equal(IntegrationAvailability.Up, instance.Availability);
        Assert.Null(instance.LastFailureAt);
    }

    [Fact]
    public void AnAttemptStillInProgressChangesNothing()
    {
        IntegrationInstance instance = Instance();

        IntegrationHealth.Apply(instance, new Invocation { OperationCode = "X", Status = InvocationStatus.InProgress, StartedAt = T });

        Assert.Equal((IntegrationAvailability.Unknown, null), (instance.Availability, instance.LastSuccessAt));
    }

    [Fact]
    public void FreshnessIsTheLastSuccessAgainstAConfiguredWindowAndUnknownWithoutOne()
    {
        TimeSpan window = TimeSpan.FromMinutes(10);

        Assert.Equal(IntegrationFreshness.Fresh, IntegrationHealth.FreshnessOf(T, window, T.AddMinutes(10)));
        Assert.Equal(IntegrationFreshness.Stale, IntegrationHealth.FreshnessOf(T, window, T.AddMinutes(10).AddTicks(1)));
        Assert.Equal(IntegrationFreshness.Unknown, IntegrationHealth.FreshnessOf(T, null, T.AddDays(30)));
        Assert.Equal(IntegrationFreshness.Unknown, IntegrationHealth.FreshnessOf(null, window, T));
    }

    private static IntegrationInstance Instance() => new() { Code = "PRIMARY", EndpointConfigurationKey = "KEY" };

    private static Invocation Attempt(InvocationStatus status, InvocationFailureCategory? category, DateTimeOffset completedAt) =>
        new() { OperationCode = "CALL", Status = status, FailureCategory = category, StartedAt = completedAt, CompletedAt = completedAt };
}
