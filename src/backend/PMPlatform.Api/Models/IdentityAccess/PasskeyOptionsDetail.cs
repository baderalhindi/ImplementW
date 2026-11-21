using System.Text.Json;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>
/// The start of a passkey ceremony: <c>options</c> for <c>navigator.credentials.create</c> or <c>.get</c>, binary values
/// base64url-encoded, and the sealed <c>transaction</c> to send back with the device's answer.
/// </summary>
public sealed record PasskeyOptionsDetail(JsonElement Options, string Transaction)
{
    internal static PasskeyOptionsDetail From(PasskeyCeremony ceremony)
    {
        ArgumentNullException.ThrowIfNull(ceremony);
        using JsonDocument options = JsonDocument.Parse(ceremony.OptionsJson);
        return new PasskeyOptionsDetail(options.RootElement.Clone(), ceremony.Transaction);
    }
}
