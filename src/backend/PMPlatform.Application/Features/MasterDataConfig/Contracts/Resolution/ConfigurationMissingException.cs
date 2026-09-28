namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

/// <summary>
/// Required configuration is absent or ambiguous, so the requesting operation stops instead of defaulting (Blueprint
/// Section 12). The API answers it 422 <c>CONFIGURATION_MISSING</c> (api-conventions §4.4). The message names the family
/// or catalogue and the entry, never a configured value.
/// </summary>
public sealed class ConfigurationMissingException : Exception
{
    public ConfigurationMissingException()
    {
        ConfigurationCode = string.Empty;
    }

    public ConfigurationMissingException(string message)
        : base(message)
    {
        ConfigurationCode = string.Empty;
    }

    public ConfigurationMissingException(string message, Exception innerException)
        : base(message, innerException)
    {
        ConfigurationCode = string.Empty;
    }

    public ConfigurationMissingException(string configurationCode, ConfigurationMissingReason reason, string? entry)
        : base(entry is null
            ? $"Configuration missing: {configurationCode} ({reason})."
            : $"Configuration missing: {configurationCode} {entry} ({reason}).")
    {
        ConfigurationCode = configurationCode;
        Reason = reason;
        Entry = entry;
    }

    /// <summary>The configuration family or master data catalogue code.</summary>
    public string ConfigurationCode { get; }

    public ConfigurationMissingReason Reason { get; }

    /// <summary>What was looked for inside it, e.g. <c>value REMINDER_OFFSET_DAYS</c>; null when the version itself is missing.</summary>
    public string? Entry { get; }
}
