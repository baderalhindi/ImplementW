using System.Globalization;
using System.Text;

namespace PMPlatform.Application.Features.Project.Import;

/// <summary>
/// How a name in a workbook is compared with a name on the platform (R3-4): trimmed, single-spaced, case-folded, without Arabic diacritics or
/// tatweel, with the alef forms as bare alef and alef maqsura as yaa. "مبسّط" and "مبسط", "الأحساء" and "الاحساء" are then one name.
/// </summary>
internal static class ImportNames
{
    public static string Key(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        StringBuilder key = new(text.Length);
        bool space = false;
        foreach (char c in text.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = key.Length > 0;
                continue;
            }

            // Harakat, shadda, sukun, superscript alef, and tatweel.
            if (c is (>= '\u064B' and <= '\u065F') or '\u0670' or '\u0640')
            {
                continue;
            }

            if (space)
            {
                key.Append(' ');
                space = false;
            }

            key.Append(c switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' => 'ي',
                _ => char.ToLower(c, CultureInfo.InvariantCulture),
            });
        }

        return key.ToString();
    }

    /// <summary>"PF-02 — وجهة ثقافية" as its code and its label; a name with no dash is its own label and has no code.</summary>
    public static (string? Code, string Label) Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (string dash in new[] { " — ", " – ", " - ", "—", "–" })
        {
            int at = text.IndexOf(dash, StringComparison.Ordinal);
            if (at > 0)
            {
                return (text[..at].Trim(), text[(at + dash.Length)..].Trim());
            }
        }

        return (null, text.Trim());
    }
}
