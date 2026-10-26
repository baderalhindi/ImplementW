using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PMPlatform.Application.Common.Observability;

/// <summary>
/// Applies the <see cref="RedactionRegistry"/> (CTL-27). A value under a registered name is replaced whole; in free text, a
/// registered name followed by <c>=</c> or <c>:</c> loses its value, and a value with a registered shape (a JWT, an e-mail
/// address, a national ID) is replaced wherever it appears. Redaction only removes: it never adds or reorders anything else.
/// </summary>
public static class TelemetryRedactor
{
    /// <summary>What a redacted value is replaced with.</summary>
    public const string Mask = "[REDACTED]";

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private static readonly string[] Names = [.. RedactionRegistry.Fields.SelectMany(field => field.Names)];

    private static readonly Regex Values = new(
        string.Join('|', RedactionRegistry.Fields.Where(field => field.ValuePattern is not null).Select(field => $"(?:{field.ValuePattern})")),
        Options,
        MatchTimeout);

    // A registered name (separators allowed between its letters, any prefix: "newPassword", "national_id") followed by = or :,
    // as in "password=…", "Authorization: …" or "\"email\":\"…\"". The value ends at the closing quote, else at a delimiter.
    private static readonly Regex NamedValues = new(
        $"""(?<![A-Za-z0-9_.-])(?<name>[A-Za-z0-9_.-]*(?:{string.Join('|', Names.Select(Spelled))}))(?<separator>["']?\s*[:=]\s*)(?:"(?<double>[^"]*)"|'(?<single>[^']*)'|(?<bare>[^\s"'&;,]+))""",
        Options,
        MatchTimeout);

    /// <summary>True when <paramref name="name"/> is, or ends with, a registered name.</summary>
    public static bool IsSensitiveName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string normalized = Normalize(name);
        return Names.Any(registered => normalized.EndsWith(registered, StringComparison.Ordinal));
    }

    /// <summary><paramref name="text"/> with every registered value replaced by <see cref="Mask"/>.</summary>
    public static string Redact(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string withoutValues = Values.Replace(text, Mask);
        return NamedValues.Replace(withoutValues, match =>
        {
            string quote = match.Groups["double"].Success ? "\"" : match.Groups["single"].Success ? "'" : string.Empty;
            return $"{match.Groups["name"].Value}{match.Groups["separator"].Value}{quote}{Mask}{quote}";
        });
    }

    /// <summary>
    /// Redacts a JSON document in place and returns it: a property with a registered name loses its whole value, and every
    /// other string is redacted as text. Numbers, booleans and the document's shape are kept.
    /// </summary>
    public static JsonNode? Redact(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject properties:
                foreach (string name in properties.Select(property => property.Key).ToList())
                {
                    if (properties[name] is { } child && Replacement(child, IsSensitiveName(name)) is { } replacement)
                    {
                        properties[name] = replacement;
                    }
                }

                return properties;
            case JsonArray items:
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] is { } item && Replacement(item, sensitive: false) is { } replacement)
                    {
                        items[i] = replacement;
                    }
                }

                return items;
            default:
                return Replacement(node, sensitive: false) ?? node;
        }
    }

    // A node attached to a parent cannot be assigned again, so a container is redacted where it is and only a value is replaced.
    private static JsonValue? Replacement(JsonNode? node, bool sensitive) => node switch
    {
        null => null,
        _ when sensitive => JsonValue.Create(Mask),
        JsonValue value when value.TryGetValue(out string? text) => JsonValue.Create(Redact(text)),
        JsonValue => null,
        _ => RedactInPlace(node),
    };

    private static JsonValue? RedactInPlace(JsonNode node)
    {
        Redact(node);
        return null;
    }

    private static string Normalize(string name)
    {
        StringBuilder normalized = new(name.Length);
        foreach (char c in name.Where(char.IsAsciiLetterOrDigit))
        {
            normalized.Append(char.ToLowerInvariant(c));
        }

        return normalized.ToString();
    }

    private static string Spelled(string name) =>
        string.Join("[-_. ]?", name.Select(c => Regex.Escape(c.ToString(CultureInfo.InvariantCulture))));
}
