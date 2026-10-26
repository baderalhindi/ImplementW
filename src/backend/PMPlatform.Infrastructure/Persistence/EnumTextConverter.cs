using System.Collections.Frozen;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>
/// Stores an enum as its member name in upper snake case — <c>UnderReview</c> as <c>UNDER_REVIEW</c> — so a state
/// column holds the value the ERD and the workbook name (ERD §6), never an integer.
/// </summary>
internal sealed class EnumTextConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => EnumText<TEnum>.ToText(value),
    text => EnumText<TEnum>.FromText(text))
    where TEnum : struct, Enum;

internal static class EnumText<TEnum>
    where TEnum : struct, Enum
{
    private static readonly FrozenDictionary<TEnum, string> Texts =
        Enum.GetValues<TEnum>().ToFrozenDictionary(value => value, value => EnumText.UpperSnakeCase(value.ToString()));

    private static readonly FrozenDictionary<string, TEnum> Values =
        Texts.ToFrozenDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    /// <summary>Every stored value, in declaration order: the value set a CHECK constraint allows.</summary>
    public static IReadOnlyList<string> All { get; } = [.. Enum.GetValues<TEnum>().Select(value => Texts[value])];

    public static string ToText(TEnum value) => Texts[value];

    public static TEnum FromText(string text) => Values[text];
}

internal static class EnumText
{
    public static string UpperSnakeCase(string pascalCase)
    {
        StringBuilder text = new(pascalCase.Length + 8);
        for (int i = 0; i < pascalCase.Length; i++)
        {
            if (i > 0 && char.IsUpper(pascalCase[i]))
            {
                text.Append('_');
            }

            text.Append(char.ToUpperInvariant(pascalCase[i]));
        }

        return text.ToString();
    }
}
