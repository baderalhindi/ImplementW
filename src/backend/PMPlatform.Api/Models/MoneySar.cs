using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
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

    /// <summary>The R-16 shape, as the OpenAPI document states it.</summary>
    public const string Pattern = @"^-?[0-9]{1,16}\.[0-9]{2}$";

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}

/// <summary>
/// R-16 in the OpenAPI document (TASK-043). <see cref="MoneySarJsonConverter"/> is opaque to the generator, which leaves a
/// <see cref="Money"/> undescribed; a request carries an amount as a <c>…Sar</c> string the API reads with
/// <see cref="MoneySar.Parse"/>. Both are stated as the decimal string they are on the wire.
/// </summary>
internal sealed class MoneySarSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);
        Type type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (type == typeof(Money))
        {
            // Nullability is the generator's: it wraps a Money? reference in oneOf with null.
            schema.Type = JsonSchemaType.String;
            schema.Pattern = MoneySar.Pattern;
        }
        else if (type == typeof(string) && context.JsonPropertyInfo?.Name.EndsWith("Sar", StringComparison.Ordinal) == true)
        {
            schema.Pattern = MoneySar.Pattern;
        }

        return Task.CompletedTask;
    }
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
