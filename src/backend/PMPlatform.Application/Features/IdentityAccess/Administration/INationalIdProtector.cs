namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// Seals National ID and Iqama numbers at rest and fingerprints them (client review, item 7), under <c>PERSONAL_DATA_KEY</c>.
/// Without that key nothing is stored or compared: the platform answers that the feature is not configured.
/// </summary>
public interface INationalIdProtector
{
    public bool IsConfigured { get; }

    public string Seal(string nationalId);

    /// <summary>The number, or null if the value was not sealed under the current key or was altered.</summary>
    public string? Unseal(string sealedValue);

    /// <summary>A keyed hash: the same number always gives the same value, and the value does not give the number back.</summary>
    public string Fingerprint(string nationalId);
}
