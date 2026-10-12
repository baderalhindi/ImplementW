using PMPlatform.Application.Features.AuditActivity;

namespace PMPlatform.Tests.Unit.Application.Auditing;

/// <summary>TASK-073 (TASK-033 F-2): the digest the chain covers changes with every attribute and with nothing else.</summary>
public sealed class AuditAttributeDigestTests
{
    private static readonly (string, string?, string?)[] Attributes = [("status", "OPEN", "CLOSED"), ("reason", null, "DONE")];

    [Fact]
    public void TheOrderTheAttributesArriveInDoesNotMatter() =>
        Assert.Equal(AuditAttributeDigest.Of(Attributes), AuditAttributeDigest.Of([.. Attributes.Reverse()]));

    [Fact]
    public void ItIsLowerCaseSha256Hex()
    {
        string digest = AuditAttributeDigest.Of(Attributes);

        Assert.Matches("^[0-9a-f]{64}$", digest);
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", AuditAttributeDigest.Of(Array.Empty<(string, string?, string?)>()));
    }

    /// <summary>Every change an attacker could make out of band gives another digest.</summary>
    public static TheoryData<string, (string, string?, string?)[]> Tampered() => new()
    {
        { "a value changed", [("status", "OPEN", "REOPENED"), ("reason", null, "DONE")] },
        { "an old value set", [("status", "OPEN", "CLOSED"), ("reason", "", "DONE")] },
        { "a value emptied", [("status", "OPEN", "CLOSED"), ("reason", null, "")] },
        { "an attribute removed", [("status", "OPEN", "CLOSED")] },
        { "an attribute added", [("status", "OPEN", "CLOSED"), ("reason", null, "DONE"), ("note", null, "x")] },
        { "an attribute renamed", [("state", "OPEN", "CLOSED"), ("reason", null, "DONE")] },
        { "values moved across the boundary", [("status", "OPEN", "CLOSED+4:DONE"), ("reason", null, null)] },
    };

    [Theory]
    [MemberData(nameof(Tampered))]
    public void AnyChangeGivesAnotherDigest(string change, (string, string?, string?)[] tampered)
    {
        Assert.NotNull(change);
        Assert.NotEqual(AuditAttributeDigest.Of(Attributes), AuditAttributeDigest.Of(tampered));
    }
}
