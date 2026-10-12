using System.Reflection;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Application.Auditing;

/// <summary>TASK-073: FG-06 §4.2 VALIDATED and §5.5 secret exclusion, as captured events are checked at capture and at ingestion.</summary>
public sealed class AuditCaptureRulesTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AWellFormedCaptureHasNoProblem() => Assert.Empty(AuditCaptureRules.Problems(Valid()));

    public static TheoryData<string, string> Malformed() => new()
    {
        { "schema", AuditActivityErrorCodes.SchemaUnsupported },
        { "event id", AuditActivityErrorCodes.EventInvalid },
        { "correlation id", AuditActivityErrorCodes.EventInvalid },
        { "class", AuditActivityErrorCodes.EventInvalid },
        { "event type", AuditActivityErrorCodes.EventTypeUnsupported },
        { "service without principal", AuditActivityErrorCodes.ActorInvalid },
        { "subject", AuditActivityErrorCodes.SourceReferenceInvalid },
        { "attribute name", AuditActivityErrorCodes.ChangeSetInvalid },
        { "duplicate attribute", AuditActivityErrorCodes.ChangeSetInvalid },
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void EachMalformationHasItsCode(string malformation, string code)
    {
        AuditCapture valid = Valid();
        AuditCapture capture = malformation switch
        {
            "schema" => valid with { SchemaVersion = 2 },
            "event id" => valid with { EventId = Guid.Empty },
            "correlation id" => valid with { CorrelationId = Guid.Empty },
            "class" => valid with { EventClass = (AuditEventClass)99 },
            "event type" => valid with { EventType = "Risk.risk_closed" },
            "service without principal" => valid with { ActorType = AuditActorType.Service, ActorUserId = null },
            "subject" => valid with { Subject = new AuditSubject("Risk", "", Guid.NewGuid()) },
            "attribute name" => valid with { Attributes = [AuditAttribute.Of("status-code", "x")] },
            "duplicate attribute" => valid with { Attributes = [AuditAttribute.Of("status", "x"), AuditAttribute.Of("status", "y")] },
            _ => throw new ArgumentOutOfRangeException(nameof(malformation)),
        };

        Assert.Equal([code], AuditCaptureRules.Problems(capture));
    }

    /// <summary>FG-06 §24.2 "Secret leakage": password, token, key and header patterns, by name and by shape.</summary>
    public static TheoryData<string, string> Secrets() => new()
    {
        { "password", "hunter2" },
        { "refresh_token", "opaque" },
        { "client_secret", "s3cr3t" },
        { "api_key", "k" },
        { "authorization_header", "Basic dXNlcjpwYXNz" },
        { "note", "Bearer abc" },
        { "note", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.signature" },
        { "note", "-----BEGIN RSA PRIVATE KEY-----\nMIIE" },
    };

    [Theory]
    [MemberData(nameof(Secrets))]
    public void ASecretLosesItsValueAndKeepsItsName(string name, string value)
    {
        AuditAttribute redacted = Assert.Single(AuditCaptureRules.Redact([new AuditAttribute(name, value, value)]));

        Assert.Equal(new AuditAttribute(name, AuditCaptureRules.Redacted, AuditCaptureRules.Redacted), redacted);
    }

    [Fact]
    public void AnAbsentValueStaysAbsent() =>
        Assert.Equal(new AuditAttribute("password", null, AuditCaptureRules.Redacted), Assert.Single(AuditCaptureRules.Redact([new AuditAttribute("password", null, "x")])));

    /// <summary>No attribute a producer declares is mistaken for a secret: redaction never removes evidence a module meant to keep.</summary>
    [Fact]
    public void NoDeclaredAttributeIsTakenForASecret()
    {
        List<string> names = [.. typeof(AuditTrail).Assembly.GetTypes()
            .Where(t => t.Name.EndsWith("AuditAttributes", StringComparison.Ordinal))
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!))];

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.Equal(new AuditAttribute(name, "v", "v"), Assert.Single(AuditCaptureRules.Redact([new AuditAttribute(name, "v", "v")]))));
    }

    private static AuditCapture Valid() =>
        new(Guid.CreateVersion7(At), AuditCapture.CurrentSchemaVersion, At, AuditEventClass.LifecycleTransition, "Risk.RiskClosed", AuditOutcome.Success,
            AuditActorType.User, Guid.NewGuid(), new AuditSubject("Risk", "Risk", Guid.NewGuid()), Guid.NewGuid(), null, null, Guid.NewGuid(),
            [AuditAttribute.Change("status", "MONITORING", "CLOSED")!]);
}
