namespace PMPlatform.Infrastructure.Reports.Rendering.Pdf;

/// <summary>
/// Display order for text that mixes Arabic and Latin (a simplified Unicode Bidirectional Algorithm, enough for labels, names and figures): the text
/// is cut into runs of right-to-left letters, left-to-right letters and digits, and neutrals; a neutral between two runs of one direction takes it,
/// any other takes the paragraph's. A right-to-left paragraph lists its runs from the right; a right-to-left run's characters are reversed and its
/// brackets mirrored. Numbers always read left to right.
/// </summary>
internal static class BidiText
{
    private static readonly HashSet<char> NumberMarks = ['%', '.', ',', ':', '/', '+', '-'];

    private static readonly Dictionary<char, char> Mirrors = new()
    {
        ['('] = ')',
        [')'] = '(',
        ['['] = ']',
        [']'] = '[',
        ['{'] = '}',
        ['}'] = '{',
        ['<'] = '>',
        ['>'] = '<',
        ['\u00AB'] = '\u00BB',
        ['\u00BB'] = '\u00AB',
    };

    private enum Direction
    {
        Neutral = 0,
        Left = 1,
        Right = 2,
    }

    /// <summary>Whether the text's first strong character is right-to-left: its paragraph direction (UAX #9 P2).</summary>
    public static bool IsRightToLeft(string text) => text.Select(Of).FirstOrDefault(d => d != Direction.Neutral) == Direction.Right;

    /// <summary>The text in display order, left to right, for a paragraph of <paramref name="rightToLeft"/> direction.</summary>
    public static string Visual(string text, bool rightToLeft)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!rightToLeft && !text.Any(c => Of(c) == Direction.Right))
        {
            return text;
        }

        List<(Direction Direction, string Text)> runs = [];
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            Direction d = Of(c);

            // A number's own separators and signs read with it, left to right (UAX #9 W4, W5): "1,250.00", "45%", "-12".
            if (d == Direction.Neutral && NumberMarks.Contains(c)
                && ((i > 0 && char.IsAsciiDigit(text[i - 1])) || (i + 1 < text.Length && char.IsAsciiDigit(text[i + 1]))))
            {
                d = Direction.Left;
            }

            if (runs.Count > 0 && runs[^1].Direction == d)
            {
                runs[^1] = (d, runs[^1].Text + c);
            }
            else
            {
                runs.Add((d, c.ToString()));
            }
        }

        // A neutral run takes the direction of the runs on both its sides when they agree, else the paragraph's.
        Direction paragraph = rightToLeft ? Direction.Right : Direction.Left;
        for (int i = 0; i < runs.Count; i++)
        {
            if (runs[i].Direction == Direction.Neutral)
            {
                Direction before = i > 0 ? runs[i - 1].Direction : paragraph;
                Direction after = i < runs.Count - 1 ? runs[i + 1].Direction : paragraph;
                runs[i] = (before == after ? before : paragraph, runs[i].Text);
            }
        }

        IEnumerable<string> ordered = runs.Select(r => r.Direction == Direction.Right ? Reverse(r.Text) : r.Text);
        return string.Concat(rightToLeft ? ordered.Reverse() : ordered);
    }

    private static string Reverse(string text) => new([.. text.Reverse().Select(c => Mirrors.GetValueOrDefault(c, c))]);

    private static Direction Of(char c) => c switch
    {
        >= '\u0590' and <= '\u08FF' when !char.IsDigit(c) && c is not ('\u060C' or '\u061B' or '\u061F') => Direction.Right,
        >= '\uFB1D' and <= '\uFDFF' => Direction.Right,
        >= '\uFE70' and <= '\uFEFF' => Direction.Right,
        '\u060C' or '\u061B' or '\u061F' => Direction.Neutral,
        _ when char.IsLetterOrDigit(c) => Direction.Left,
        _ => Direction.Neutral,
    };
}
