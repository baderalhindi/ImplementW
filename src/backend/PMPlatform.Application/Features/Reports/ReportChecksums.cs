using System.Security.Cryptography;

namespace PMPlatform.Application.Features.Reports;

/// <summary>An output's integrity hash (FG-02 GEN-007): SHA-256 in lower-case hex, recorded at generation and checked before every download.</summary>
internal static class ReportChecksums
{
    public static string Sha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
}
