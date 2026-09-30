using System.Text;
using System.Text.RegularExpressions;

namespace PMPlatform.Application.Features.Notifications;

/// <summary>
/// Template text: literal text with <c>{{name}}</c> placeholders, a name being a letter followed by letters and digits.
/// Rendering substitutes plain text; nothing is interpreted, so a parameter value cannot become markup or a header
/// (every channel carries plain text). Any other <c>{{</c> or <c>}}</c> is malformed.
/// </summary>
internal static partial class TemplateText
{
    /// <summary>The placeholder every template may use: the intent's deep link, absolute for e-mail and SMS.</summary>
    public const string DeepLink = "deepLink";

    public static bool IsWellFormed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string rest = Placeholder().Replace(text, string.Empty);
        return !rest.Contains("{{", StringComparison.Ordinal) && !rest.Contains("}}", StringComparison.Ordinal);
    }

    /// <summary>The distinct placeholder names in the text, in order of first use.</summary>
    public static IReadOnlyList<string> Placeholders(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. Placeholder().Matches(text).Select(m => m.Groups["name"].Value).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The text with every placeholder replaced; null when a placeholder has no value.</summary>
    public static string? Render(string text, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(values);
        if (Placeholders(text).Any(name => !values.ContainsKey(name)))
        {
            return null;
        }

        StringBuilder rendered = new(text.Length);
        int position = 0;
        foreach (Match match in Placeholder().Matches(text))
        {
            rendered.Append(text, position, match.Index - position).Append(values[match.Groups["name"].Value]);
            position = match.Index + match.Length;
        }

        return rendered.Append(text, position, text.Length - position).ToString();
    }

    [GeneratedRegex(@"\{\{(?<name>[A-Za-z][A-Za-z0-9]*)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
