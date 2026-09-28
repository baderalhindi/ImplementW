using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Application.Auditing;

/// <summary>How values are written into the audit store, and the two ways a producer keeps one out of it.</summary>
public sealed class AuditValueTests
{
    [Fact]
    public void ValuesAreWrittenAsTheApiAndTheDatabaseWriteThem()
    {
        Assert.Equal("AUTHORIZATION_DENIAL", AuditValue.Format(AuditEventClass.AuthorizationDenial));
        Assert.Equal("00000000-0000-4000-8000-000000000001", AuditValue.Format(Guid.Parse("00000000-0000-4000-8000-000000000001")));
        Assert.Equal("2026-09-28T06:00:00.0000000Z", AuditValue.Format(new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.FromHours(3))));
        Assert.Equal("false", AuditValue.Format(false));
        Assert.Null(AuditValue.Format(null));
    }

    [Fact]
    public void AnUnchangedValueIsNoChange()
    {
        Assert.Null(AuditAttribute.Change("status", "ACTIVE", "ACTIVE"));
        Assert.Equal(new AuditAttribute("status", "ACTIVE", "DISABLED"), AuditAttribute.Change("status", "ACTIVE", "DISABLED"));
    }

    /// <summary>A personal value that changed is recorded as changed; neither the old nor the new value is copied.</summary>
    [Fact]
    public void AWithheldChangeCarriesNeitherValue()
    {
        Assert.Equal(new AuditAttribute("email", AuditAttribute.Withheld, AuditAttribute.Withheld), AuditAttribute.WithheldChange("email", "a@x.test", "b@x.test"));
        Assert.Equal(new AuditAttribute("mobile_number", null, AuditAttribute.Withheld), AuditAttribute.WithheldChange("mobile_number", null, "+966500000001"));
        Assert.Null(AuditAttribute.WithheldChange("email", "a@x.test", "a@x.test"));
    }

    [Fact]
    public void AForwardedClassWithATypingErrorIsReported()
    {
        SiemForwardingPolicy policy = new();
        policy.ForwardedClasses.Add("AUTHENTICATION");
        policy.ForwardedClasses.Add("AUTHENTICATON");

        Assert.Equal(["AUTHENTICATON"], policy.UnknownClasses());
        Assert.True(policy.Forwards(AuditEventClass.Authentication));
        Assert.False(policy.Forwards(AuditEventClass.PrivilegedAction));
    }
}
