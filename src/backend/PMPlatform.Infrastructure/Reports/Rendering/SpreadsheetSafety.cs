namespace PMPlatform.Infrastructure.Reports.Rendering;

/// <summary>
/// FG-02 §9.2 and BR-RPT-039: text a spreadsheet could read as a formula — one whose first character, after leading whitespace and control
/// characters, is =, +, -, @ or their full-width forms, or that begins with a tab or a carriage return (OWASP CSV injection) — is never written as
/// such. A value the source typed as a number is a number, not text, and is written as one.
/// </summary>
internal static class SpreadsheetSafety
{
    private static readonly char[] Triggers = ['=', '+', '-', '@', '\uFF1D', '\uFF0B', '\uFF0D', '\uFF20'];

    public static bool IsFormulaLike(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > 0 && text[0] is '\t' or '\r')
        {
            return true;
        }

        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                continue;
            }

            return Triggers.Contains(c);
        }

        return false;
    }

    /// <summary>
    /// The text a spreadsheet keeps as text: prefixed with an apostrophe when formula-like (the OWASP treatment), with control characters other than
    /// a line break dropped.
    /// </summary>
    public static string Neutralize(string text, out bool neutralized)
    {
        ArgumentNullException.ThrowIfNull(text);
        neutralized = IsFormulaLike(text);
        string clean = new([.. text.Where(c => !char.IsControl(c) || c == '\n')]);
        return neutralized ? "'" + clean : clean;
    }
}
