using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models;

/// <summary>
/// api-conventions R-16: a SAR amount crosses the wire as a decimal string with exactly two fraction digits, e.g.
/// <c>"12500000.00"</c>, never a JSON number (a numeric(18,2) exceeds what a JavaScript number holds exactly).
/// </summary>
internal static partial class MoneySar
{
    public static string Format(Money value) => value.Amount.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>The amount, or null if <paramref name="text"/> is not an R-16 decimal string within numeric(18,2).</summary>
    public static Money? Parse(string text) =>
        Shape().IsMatch(text) && decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount)
            ? new Money(amount)
            : null;

    [GeneratedRegex(@"^-?[0-9]{1,16}\.[0-9]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}

/// <summary><see cref="MoneySar"/> for every <see cref="Money"/> the API writes or reads.</summary>
internal sealed class MoneySarJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && MoneySar.Parse(reader.GetString()!) is { } amount
            ? amount
            : throw new JsonException("A SAR amount is a decimal string with two fraction digits.");

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(MoneySar.Format(value));
    }
}
