using PMPlatform.Application.Features.IdentityAccess.Administration;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>A reversible stand-in: the administration rules are under test here, not the cryptography.</summary>
internal sealed class FakeNationalIdProtector : INationalIdProtector
{
    public bool IsConfigured => true;

    public string Seal(string nationalId) => "sealed:" + nationalId;

    public string? Unseal(string sealedValue) => sealedValue.StartsWith("sealed:", StringComparison.Ordinal) ? sealedValue["sealed:".Length..] : null;

    public string Fingerprint(string nationalId) => "fp:" + nationalId;
}
