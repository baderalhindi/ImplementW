namespace PMPlatform.Infrastructure.Reports.Rendering.Pdf;

/// <summary>
/// Arabic contextual shaping for a font drawn glyph by glyph (FG-02 §9.1: Arabic RTL output): each letter takes its isolated, initial, medial or
/// final form from Unicode's Presentation Forms-B by whether it joins the letters beside it, and LAM followed by ALEF becomes their ligature.
/// Text is shaped in logical order, before it is reordered for display. Diacritics are transparent to joining. Letters outside the basic Arabic
/// block are left as they are.
/// </summary>
internal static class ArabicShaping
{
    private const char Lam = '\u0644';

    /// <summary>The presentation forms of each letter: isolated, final, initial, medial; a right-joining letter has no initial or medial form.</summary>
    private static readonly Dictionary<char, char[]> Forms = new()
    {
        ['\u0621'] = ['\uFE80'],
        ['\u0622'] = ['\uFE81', '\uFE82'],
        ['\u0623'] = ['\uFE83', '\uFE84'],
        ['\u0624'] = ['\uFE85', '\uFE86'],
        ['\u0625'] = ['\uFE87', '\uFE88'],
        ['\u0626'] = ['\uFE89', '\uFE8A', '\uFE8B', '\uFE8C'],
        ['\u0627'] = ['\uFE8D', '\uFE8E'],
        ['\u0628'] = ['\uFE8F', '\uFE90', '\uFE91', '\uFE92'],
        ['\u0629'] = ['\uFE93', '\uFE94'],
        ['\u062A'] = ['\uFE95', '\uFE96', '\uFE97', '\uFE98'],
        ['\u062B'] = ['\uFE99', '\uFE9A', '\uFE9B', '\uFE9C'],
        ['\u062C'] = ['\uFE9D', '\uFE9E', '\uFE9F', '\uFEA0'],
        ['\u062D'] = ['\uFEA1', '\uFEA2', '\uFEA3', '\uFEA4'],
        ['\u062E'] = ['\uFEA5', '\uFEA6', '\uFEA7', '\uFEA8'],
        ['\u062F'] = ['\uFEA9', '\uFEAA'],
        ['\u0630'] = ['\uFEAB', '\uFEAC'],
        ['\u0631'] = ['\uFEAD', '\uFEAE'],
        ['\u0632'] = ['\uFEAF', '\uFEB0'],
        ['\u0633'] = ['\uFEB1', '\uFEB2', '\uFEB3', '\uFEB4'],
        ['\u0634'] = ['\uFEB5', '\uFEB6', '\uFEB7', '\uFEB8'],
        ['\u0635'] = ['\uFEB9', '\uFEBA', '\uFEBB', '\uFEBC'],
        ['\u0636'] = ['\uFEBD', '\uFEBE', '\uFEBF', '\uFEC0'],
        ['\u0637'] = ['\uFEC1', '\uFEC2', '\uFEC3', '\uFEC4'],
        ['\u0638'] = ['\uFEC5', '\uFEC6', '\uFEC7', '\uFEC8'],
        ['\u0639'] = ['\uFEC9', '\uFECA', '\uFECB', '\uFECC'],
        ['\u063A'] = ['\uFECD', '\uFECE', '\uFECF', '\uFED0'],
        ['\u0641'] = ['\uFED1', '\uFED2', '\uFED3', '\uFED4'],
        ['\u0642'] = ['\uFED5', '\uFED6', '\uFED7', '\uFED8'],
        ['\u0643'] = ['\uFED9', '\uFEDA', '\uFEDB', '\uFEDC'],
        ['\u0644'] = ['\uFEDD', '\uFEDE', '\uFEDF', '\uFEE0'],
        ['\u0645'] = ['\uFEE1', '\uFEE2', '\uFEE3', '\uFEE4'],
        ['\u0646'] = ['\uFEE5', '\uFEE6', '\uFEE7', '\uFEE8'],
        ['\u0647'] = ['\uFEE9', '\uFEEA', '\uFEEB', '\uFEEC'],
        ['\u0648'] = ['\uFEED', '\uFEEE'],
        ['\u0649'] = ['\uFEEF', '\uFEF0'],
        ['\u064A'] = ['\uFEF1', '\uFEF2', '\uFEF3', '\uFEF4'],
    };

    /// <summary>LAM + ALEF ligatures: isolated, final.</summary>
    private static readonly Dictionary<char, char[]> LamAlef = new()
    {
        ['\u0622'] = ['\uFEF5', '\uFEF6'],
        ['\u0623'] = ['\uFEF7', '\uFEF8'],
        ['\u0625'] = ['\uFEF9', '\uFEFA'],
        ['\u0627'] = ['\uFEFB', '\uFEFC'],
    };

    public static string Shape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.Any(Forms.ContainsKey))
        {
            return text;
        }

        System.Text.StringBuilder shaped = new(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (!Forms.TryGetValue(c, out char[]? forms))
            {
                shaped.Append(c);
                continue;
            }

            bool joinsBefore = Previous(text, i) is { } before && JoinsForward(before);
            int next = Next(text, i);
            if (c == Lam && next >= 0 && LamAlef.TryGetValue(text[next], out char[]? ligature))
            {
                shaped.Append(ligature[joinsBefore ? 1 : 0]);
                shaped.Append(text, i + 1, next - i - 1);
                i = next;
                continue;
            }

            bool joinsAfter = forms.Length == 4 && next >= 0 && (text[next] == '\u0640' || (Forms.TryGetValue(text[next], out char[]? after) && after.Length > 1));
            shaped.Append((joinsBefore, joinsAfter, forms.Length) switch
            {
                (true, true, 4) => forms[3],
                (false, true, 4) => forms[2],
                (true, _, > 1) => forms[1],
                _ => forms[0],
            });
        }

        return shaped.ToString();
    }

    /// <summary>Whether a letter connects to the one after it: a dual-joining letter or a tatweel.</summary>
    private static bool JoinsForward(char c) => c == '\u0640' || (Forms.TryGetValue(c, out char[]? forms) && forms.Length == 4);

    private static char? Previous(string text, int i)
    {
        for (int j = i - 1; j >= 0; j--)
        {
            if (!IsTransparent(text[j]))
            {
                return text[j];
            }
        }

        return null;
    }

    private static int Next(string text, int i)
    {
        for (int j = i + 1; j < text.Length; j++)
        {
            if (!IsTransparent(text[j]))
            {
                return j;
            }
        }

        return -1;
    }

    /// <summary>Harakat and other combining marks do not interrupt joining.</summary>
    private static bool IsTransparent(char c) => c is (>= '\u064B' and <= '\u065F') or '\u0670';
}
