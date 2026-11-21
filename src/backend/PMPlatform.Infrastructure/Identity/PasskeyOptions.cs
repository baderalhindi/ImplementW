using Microsoft.Extensions.Configuration;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// <c>Identity:Passkeys</c>: the platform as a WebAuthn relying party (13 Nov 2026 review). A passkey is bound to the
/// relying party id, the site's domain: passkeys made on one domain never work on another, so changing it after people
/// have registered passkeys makes them register again. Unset, both come from <c>APP_BASE_URL</c>.
/// </summary>
internal sealed class PasskeyOptions
{
    public const string Section = "Identity:Passkeys";

    public const string AppBaseUrlKey = "APP_BASE_URL";

    /// <summary>The domain passkeys belong to, such as <c>pmplatform.ahda.gov.sa</c>; the host of the first origin when unset.</summary>
    public string? RelyingPartyId { get; set; }

    /// <summary>The name the device shows when the person creates a passkey.</summary>
    public string RelyingPartyName { get; set; } = "AHDA PMPlatform";

    /// <summary>The origins the SPA is served from (<c>scheme://host[:port]</c>); <c>APP_BASE_URL</c> when empty.</summary>
    public IList<string> Origins { get; } = [];

    /// <summary>How long the device has to answer.</summary>
    public TimeSpan CeremonyLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>The relying party this environment has, or null if no origin is known.</summary>
    public PasskeyRelyingPartyIdentity? Resolve(IConfiguration configuration)
    {
        List<string> origins = [.. Origins.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim().TrimEnd('/'))];
        if (origins.Count == 0 && configuration[AppBaseUrlKey] is { Length: > 0 } appBaseUrl
            && Uri.TryCreate(appBaseUrl, UriKind.Absolute, out Uri? baseUri))
        {
            origins.Add(baseUri.GetLeftPart(UriPartial.Authority));
        }

        if (origins.Count == 0 || !Uri.TryCreate(origins[0], UriKind.Absolute, out Uri? first))
        {
            return null;
        }

        string relyingPartyId = string.IsNullOrWhiteSpace(RelyingPartyId) ? first.Host : RelyingPartyId.Trim();
        return new PasskeyRelyingPartyIdentity(relyingPartyId, RelyingPartyName, origins);
    }
}

internal sealed record PasskeyRelyingPartyIdentity(string Id, string Name, IReadOnlyList<string> Origins);
