using System.Buffers.Binary;
using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// A software platform authenticator, as Face ID or Touch ID answers a browser: one ES256 passkey, made for the relying party
/// and origin it is given, with user presence and user verification set. Its answers are real WebAuthn messages, so the
/// API verifies them exactly as it would a phone's.
/// </summary>
internal sealed class TestPasskeyDevice(string origin, string relyingPartyId) : IDisposable
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly byte[] _credentialId = RandomNumberGenerator.GetBytes(16);
    private byte[] _userHandle = [];
    private uint _counter;

    /// <summary>Flags: user present (0x01), user verified (0x04), attested credential data (0x40).</summary>
    private const byte RegistrationFlags = 0x45;

    private const byte AssertionFlags = 0x05;

    /// <summary>Answers <c>navigator.credentials.create</c> with a new passkey, attestation "none".</summary>
    public JsonObject Create(JsonElement options)
    {
        _userHandle = Base64Url.DecodeFromChars(options.GetProperty("user").GetProperty("id").GetString());
        byte[] clientData = ClientData("webauthn.create", options.GetProperty("challenge").GetString()!);
        byte[] authenticatorData = [.. RelyingPartyIdHash(), RegistrationFlags, .. Counter(), .. new byte[16], .. Length(_credentialId), .. _credentialId, .. PublicKey()];

        CborWriter attestation = new();
        attestation.WriteStartMap(3);
        attestation.WriteTextString("fmt");
        attestation.WriteTextString("none");
        attestation.WriteTextString("attStmt");
        attestation.WriteStartMap(0);
        attestation.WriteEndMap();
        attestation.WriteTextString("authData");
        attestation.WriteByteString(authenticatorData);
        attestation.WriteEndMap();

        return Credential(new JsonObject
        {
            ["clientDataJSON"] = Base64Url.EncodeToString(clientData),
            ["attestationObject"] = Base64Url.EncodeToString(attestation.Encode()),
            ["transports"] = new JsonArray("internal"),
        });
    }

    /// <summary>Answers <c>navigator.credentials.get</c>, signing with the passkey and moving its counter on.</summary>
    public JsonObject Get(JsonElement options, Func<byte[], byte[]>? tamper = null)
    {
        _counter++;
        byte[] clientData = ClientData("webauthn.get", options.GetProperty("challenge").GetString()!);
        byte[] authenticatorData = [.. RelyingPartyIdHash(), AssertionFlags, .. Counter()];
        byte[] signature = _key.SignData([.. authenticatorData, .. SHA256.HashData(clientData)], HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        return Credential(new JsonObject
        {
            ["clientDataJSON"] = Base64Url.EncodeToString(clientData),
            ["authenticatorData"] = Base64Url.EncodeToString(authenticatorData),
            ["signature"] = Base64Url.EncodeToString(tamper is null ? signature : tamper(signature)),
            ["userHandle"] = Base64Url.EncodeToString(_userHandle),
        });
    }

    public void Dispose() => _key.Dispose();

    private JsonObject Credential(JsonObject response) => new()
    {
        ["id"] = Base64Url.EncodeToString(_credentialId),
        ["rawId"] = Base64Url.EncodeToString(_credentialId),
        ["type"] = "public-key",
        ["response"] = response,
        ["clientExtensionResults"] = new JsonObject(),
    };

    private byte[] ClientData(string type, string challenge) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type, challenge, origin, crossOrigin = false }));

    private byte[] RelyingPartyIdHash() => SHA256.HashData(Encoding.UTF8.GetBytes(relyingPartyId));

    private byte[] Counter()
    {
        byte[] value = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(value, _counter);
        return value;
    }

    private static byte[] Length(byte[] value)
    {
        byte[] length = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)value.Length);
        return length;
    }

    /// <summary>The COSE_Key of an ES256 public key (RFC 9053): kty EC2, alg -7, crv P-256, x, y.</summary>
    private byte[] PublicKey()
    {
        ECParameters parameters = _key.ExportParameters(includePrivateParameters: false);
        CborWriter cose = new(CborConformanceMode.Ctap2Canonical);
        cose.WriteStartMap(5);
        cose.WriteInt32(1);
        cose.WriteInt32(2);
        cose.WriteInt32(3);
        cose.WriteInt32(-7);
        cose.WriteInt32(-1);
        cose.WriteInt32(1);
        cose.WriteInt32(-2);
        cose.WriteByteString(parameters.Q.X!);
        cose.WriteInt32(-3);
        cose.WriteByteString(parameters.Q.Y!);
        cose.WriteEndMap();
        return cose.Encode();
    }
}
