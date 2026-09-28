using System.Security.Cryptography;
using System.Text.Json;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>
/// The SHA-256 of a content's JSON in canonical order, recorded in the audit event of each replacement: which content was replaced by
/// which, without copying configuration into the audit store. A PUBLISHED version's content is kept in full by the
/// version itself.
/// </summary>
internal static class ContentFingerprint
{
    public static string Of(ConfigurationContent content) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(ContentOrder.Canonical(content))));
}
