using PMPlatform.Application.Features.IntegrationMonitoring;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Tests.Unit.Application.IntegrationMonitoring;

/// <summary>
/// FG-05 §7 under a definition's policy: a technical failure is retried after a back-off that doubles until the attempts are spent, then
/// dead-lettered; a security refusal needs a person at once; a business refusal is the source's to correct and ends FAILED.
/// </summary>
public sealed class IntegrationRetryPolicyTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    private static readonly IntegrationDefinition Definition = new() { Code = "TEST", Name = new BilingualLabel("اختبار", "Test"), MaxAttempts = 3, RetryBackoffSeconds = 60 };

    [Theory]
    [InlineData(1, 60)]
    [InlineData(2, 120)]
    public void ATechnicalFailureIsRetriedAfterADoublingBackoff(short attemptNo, int seconds) =>
        Assert.Equal(
            (InvocationStatus.RetryScheduled, T.AddSeconds(seconds)),
            IntegrationRetryPolicy.After(Definition, attemptNo, InvocationFailureCategory.Technical, T));

    [Fact]
    public void TheLastAttemptsTechnicalFailureIsDeadLettered() =>
        Assert.Equal((InvocationStatus.DeadLetter, null), IntegrationRetryPolicy.After(Definition, 3, InvocationFailureCategory.Technical, T));

    [Fact]
    public void ASecurityRefusalIsDeadLetteredAtOnce() =>
        Assert.Equal((InvocationStatus.DeadLetter, null), IntegrationRetryPolicy.After(Definition, 1, InvocationFailureCategory.Security, T));

    [Fact]
    public void ABusinessRefusalIsFailedAndNotRetried() =>
        Assert.Equal((InvocationStatus.Failed, null), IntegrationRetryPolicy.After(Definition, 1, InvocationFailureCategory.BusinessValidation, T));
}
