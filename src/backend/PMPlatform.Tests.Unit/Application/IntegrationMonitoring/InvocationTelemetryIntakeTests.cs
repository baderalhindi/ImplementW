using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IntegrationMonitoring;

namespace PMPlatform.Tests.Unit.Application.IntegrationMonitoring;

/// <summary>The telemetry contract FG-05 holds producers to (event-conventions §4 rows 3–5): what makes an observed attempt unusable.</summary>
public sealed class InvocationTelemetryIntakeTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    private static readonly IntegrationInvocationData Valid = new(
        "EXCHANGE_EMAIL", "SEND", "delivery-1", IntegrationInvocationOutcome.Failed, IntegrationFailureKind.Technical, IntegrationRetryDisposition.RetryScheduled,
        T, T.AddSeconds(1), null, "SocketException", T.AddMinutes(1), "NotificationDelivery/1");

    public static TheoryData<string> Broken() =>
    [
        "integration", "operation", "lineage", "failure kind", "success followed", "next attempt", "ends before",
    ];

    [Fact]
    public void AValidAttemptHasNoProblem() => Assert.Null(InvocationTelemetryIntake.Problem(Valid));

    [Theory]
    [MemberData(nameof(Broken))]
    public void ABrokenAttemptIsRefused(string broken)
    {
        IntegrationInvocationData data = broken switch
        {
            "integration" => Valid with { IntegrationCode = "exchange email" },
            "operation" => Valid with { OperationCode = "" },
            "lineage" => Valid with { LineageKey = new string('k', 201) },
            "failure kind" => Valid with { FailureKind = null },
            "success followed" => Valid with { Outcome = IntegrationInvocationOutcome.Succeeded, FailureKind = null },
            "next attempt" => Valid with { NextAttemptAt = null },
            "ends before" => Valid with { CompletedAt = T.AddSeconds(-1) },
            _ => throw new ArgumentOutOfRangeException(nameof(broken)),
        };

        Assert.NotNull(InvocationTelemetryIntake.Problem(data));
    }
}
