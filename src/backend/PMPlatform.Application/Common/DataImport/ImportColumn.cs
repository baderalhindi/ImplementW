using System.Text.Json;

namespace PMPlatform.Application.Common.DataImport;

/// <summary>What a cell of a column must hold. The engine checks the shape; the register checks the meaning.</summary>
public enum ImportValueKind
{
    Text = 1,

    /// <summary>A business code: upper-case letters, digits, hyphen and underscore.</summary>
    Code = 2,

    Email = 3,

    /// <summary>E.164 (ADR-004).</summary>
    PhoneNumber = 4,

    /// <summary>ISO 8601 <c>yyyy-MM-dd</c>, or a date cell.</summary>
    Date = 5,

    Number = 6,

    WholeNumber = 7,

    /// <summary><c>TRUE</c> or <c>FALSE</c>, or a boolean cell.</summary>
    Boolean = 8,

    /// <summary>One of <see cref="ImportColumn.Choices"/>.</summary>
    Choice = 9,

    /// <summary>A SAR amount (ADR-008): not negative, at most two decimal places, within numeric(18,2).</summary>
    Money = 10,
}

/// <summary>
/// One column of a register's workbook: the header a cell sits under, whether a row must fill it, and what it must hold. The
/// descriptions go into the template's Columns sheet in both languages (ADR-012).
/// </summary>
public sealed record ImportColumn(string Key, ImportValueKind Kind, bool Required, string DescriptionEn, string DescriptionAr)
{
    /// <summary>The header every workbook starts with: the record's identifier in the legacy system.</summary>
    public const string LegacyIdKey = "legacy_id";

    public const int LegacyIdLength = 100;

    /// <summary>The longest value a cell may hold; a code's default is the platform's code length.</summary>
    public int MaxLength { get; init; } = Kind == ImportValueKind.Code ? 50 : 200;

    /// <summary>The values a <see cref="ImportValueKind.Choice"/> column accepts, as the template lists them.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>A choice column of an enum's values, each as the API writes it: <c>UnderReview</c> as <c>UNDER_REVIEW</c>.</summary>
    public static ImportColumn ChoiceOf<TEnum>(string key, bool required, string descriptionEn, string descriptionAr, params TEnum[] values)
        where TEnum : struct, Enum =>
        new(key, ImportValueKind.Choice, required, descriptionEn, descriptionAr)
        {
            Choices = [.. (values.Length > 0 ? values : Enum.GetValues<TEnum>()).Select(v => ChoiceText(v))],
        };

    public static string ChoiceText<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());

    public static ImportColumn LegacyId { get; } = new(
        LegacyIdKey, ImportValueKind.Text, Required: true,
        "The record's identifier in the legacy system. Unique in the register; a row whose legacy ID was already imported is skipped.",
        "معرّف السجل في النظام السابق. فريد في السجل؛ يُتجاوز الصف الذي سبق استيراد معرّفه.")
    {
        MaxLength = LegacyIdLength,
    };
}
