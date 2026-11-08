using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// AES-256-GCM for the number and HMAC-SHA256 for its fingerprint, each under its own key derived with HKDF from <c>PERSONAL_DATA_KEY</c>,
/// so the fingerprint key never opens a sealed number. A sealed value is <c>v1.</c> and base64url of nonce, tag and ciphertext.
/// </summary>
internal sealed class NationalIdProtector(IConfiguration configuration) : INationalIdProtector
{
    private const int MinimumKeyLength = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string Version = "v1.";
    private static readonly byte[] SealInfo = Encoding.ASCII.GetBytes("PMPlatform national id seal v1");
    private static readonly byte[] FingerprintInfo = Encoding.ASCII.GetBytes("PMPlatform national id fingerprint v1");

    public bool IsConfigured => configuration[ApplicationSecrets.PersonalDataKey] is { Length: >= MinimumKeyLength };

    public string Seal(string nationalId)
    {
        byte[] plaintext = Encoding.ASCII.GetBytes(nationalId);
        byte[] sealedBytes = new byte[NonceSize + TagSize + plaintext.Length];
        RandomNumberGenerator.Fill(sealedBytes.AsSpan(0, NonceSize));
        using AesGcm aes = new(Key(SealInfo), TagSize);
        aes.Encrypt(sealedBytes.AsSpan(0, NonceSize), plaintext, sealedBytes.AsSpan(NonceSize + TagSize), sealedBytes.AsSpan(NonceSize, TagSize));
        return Version + Base64Url.EncodeToString(sealedBytes);
    }

    public string? Unseal(string sealedValue)
    {
        ArgumentNullException.ThrowIfNull(sealedValue);
        if (!sealedValue.StartsWith(Version, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            byte[] sealedBytes = Base64Url.DecodeFromChars(sealedValue.AsSpan(Version.Length));
            if (sealedBytes.Length <= NonceSize + TagSize)
            {
                return null;
            }

            byte[] plaintext = new byte[sealedBytes.Length - NonceSize - TagSize];
            using AesGcm aes = new(Key(SealInfo), TagSize);
            aes.Decrypt(sealedBytes.AsSpan(0, NonceSize), sealedBytes.AsSpan(NonceSize + TagSize), sealedBytes.AsSpan(NonceSize, TagSize), plaintext);
            return Encoding.ASCII.GetString(plaintext);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return null;
        }
    }

    public string Fingerprint(string nationalId) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Key(FingerprintInfo), Encoding.ASCII.GetBytes(nationalId)));

    private byte[] Key(byte[] info)
    {
        string secret = configuration[ApplicationSecrets.PersonalDataKey] is { Length: >= MinimumKeyLength } value
            ? value
            : throw new InvalidOperationException($"{ApplicationSecrets.PersonalDataKey} is not configured.");
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secret), 32, salt: [], info);
    }
}
