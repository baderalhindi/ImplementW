using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// Seals an <see cref="AuthorizationTransaction"/> with AES-256-GCM so the client can hold it without reading or altering it.
/// No server-side store is needed, so any replica completes an authorization any other replica started. The key is
/// derived from <c>JWT_SIGNING_KEY</c> with HKDF under <paramref name="label"/>, so it is never the token-signing key
/// itself, and each flow's label gives it a key of its own: an SSO transaction cannot be opened as a Nafath one.
/// </summary>
internal sealed class AuthorizationTransactionProtector(IConfiguration configuration, string label)
{
    /// <summary>The label of SSO sign-in transactions (TASK-028), unchanged so a sign-in under way survives a deployment.</summary>
    public const string SingleSignOnLabel = "PMPlatform SSO transaction v1";

    /// <summary>The label of Nafath identity verification transactions (TASK-068).</summary>
    public const string NafathLabel = "PMPlatform Nafath transaction v1";

    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _keyLabel = Encoding.ASCII.GetBytes(label);

    public string Protect(AuthorizationTransaction transaction)
    {
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(transaction);
        byte[] sealedBytes = new byte[NonceSize + TagSize + plaintext.Length];
        Span<byte> nonce = sealedBytes.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);

        using AesGcm aes = new(Key(), TagSize);
        aes.Encrypt(nonce, plaintext, sealedBytes.AsSpan(NonceSize + TagSize), sealedBytes.AsSpan(NonceSize, TagSize));
        return Base64Url.EncodeToString(sealedBytes);
    }

    /// <summary>The transaction, or null if the value was not sealed under this label by this platform's current key, or was altered.</summary>
    public AuthorizationTransaction? Unprotect(string value)
    {
        try
        {
            byte[] sealedBytes = Base64Url.DecodeFromChars(value);
            if (sealedBytes.Length <= NonceSize + TagSize)
            {
                return null;
            }

            byte[] plaintext = new byte[sealedBytes.Length - NonceSize - TagSize];
            using AesGcm aes = new(Key(), TagSize);
            aes.Decrypt(sealedBytes.AsSpan(0, NonceSize), sealedBytes.AsSpan(NonceSize + TagSize), sealedBytes.AsSpan(NonceSize, TagSize), plaintext);
            return JsonSerializer.Deserialize<AuthorizationTransaction>(plaintext);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or JsonException)
        {
            return null;
        }
    }

    private byte[] Key() => HKDF.DeriveKey(HashAlgorithmName.SHA256, SessionSigningKey.ReadBytes(configuration), 32, salt: [], info: _keyLabel);
}
