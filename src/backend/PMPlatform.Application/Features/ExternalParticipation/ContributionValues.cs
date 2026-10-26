using System.Globalization;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// The answered values against their schema (WF-13 EXT-CC-06): a field outside it, or given twice, is refused (BR-EXT-020); a value must
/// be of its field's type and range; a narrative carries its language, nothing else does. Accepted values are kept in canonical text — a
/// number in invariant notation without trailing zeros, a date as ISO 8601 — so the same answer is always stored the same way.
/// </summary>
internal static class ContributionValues
{
    /// <summary>A number's fraction digits, as WF-04 keeps a percentage (ADR-009).</summary>
    public const int MaxFractionDigits = 4;

    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>The values in canonical form, in the order given; or the first refusal, naming every field it concerns.</summary>
    public static AdministrationResult<IReadOnlyList<ContributionFieldValue>> Normalize(ContributionSchema schema, IReadOnlyList<ContributionFieldInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(inputs);

        List<FieldIssue> outside = [];
        List<FieldIssue> invalid = [];
        List<ContributionFieldValue> values = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < inputs.Count; i++)
        {
            ContributionFieldInput input = inputs[i];
            if (schema.Field(input.FieldCode) is not { } field)
            {
                outside.Add(new FieldIssue($"fields[{i}].fieldCode", FieldIssue.NotAllowed));
                continue;
            }

            if (!seen.Add(input.FieldCode))
            {
                outside.Add(new FieldIssue($"fields[{i}].fieldCode", FieldIssue.Duplicate));
                continue;
            }

            if (Canonical(field, input) is { } canonical)
            {
                values.Add(new ContributionFieldValue(field.FieldCode, canonical, field.FieldType == ContributionFieldType.Narrative ? input.Language : null));
            }
            else
            {
                invalid.Add(new FieldIssue($"fields[{i}].value", FieldIssue.NotAllowed));
            }
        }

        return outside.Count > 0 ? AdministrationError.Rule(ExternalParticipationErrorCodes.FieldNotAllowed, [.. outside])
            : invalid.Count > 0 ? AdministrationError.Rule(ExternalParticipationErrorCodes.ValidationFailed, [.. invalid])
            : values;
    }

    /// <summary>422 <c>CONTRIBUTION_REQUIRED_ITEM_MISSING</c> naming each required field without a value; null when every one has one.</summary>
    public static AdministrationError? MissingRequired(ContributionSchema schema, IEnumerable<string> answered)
    {
        ArgumentNullException.ThrowIfNull(schema);
        HashSet<string> present = [.. answered];
        FieldIssue[] missing = [.. schema.Fields.Where(f => f.Required && !present.Contains(f.FieldCode)).Select(f => new FieldIssue(f.FieldCode, FieldIssue.Required))];
        return missing.Length > 0 ? AdministrationError.Rule(ExternalParticipationErrorCodes.RequiredItemMissing, missing) : null;
    }

    /// <summary>A stored number value, as <see cref="Normalize"/> wrote it.</summary>
    public static decimal NumberOf(string canonical) => decimal.Parse(canonical, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    private static string? Canonical(ContributionFieldDefinition field, ContributionFieldInput input)
    {
        bool narrative = field.FieldType == ContributionFieldType.Narrative;
        return narrative != input.Language.HasValue || string.IsNullOrWhiteSpace(input.Value) ? null : field.FieldType switch
        {
            ContributionFieldType.Narrative => input.Value,
            ContributionFieldType.Number => decimal.TryParse(input.Value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal number)
                                            && number.Scale <= MaxFractionDigits
                                            && (field.Minimum is not { } minimum || number >= minimum)
                                            && (field.Maximum is not { } maximum || number <= maximum)
                ? number.ToString("0.####", CultureInfo.InvariantCulture)
                : null,
            ContributionFieldType.Date => DateOnly.TryParseExact(input.Value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
                ? date.ToString(DateFormat, CultureInfo.InvariantCulture)
                : null,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field.FieldType, "Unknown field type."),
        };
    }
}
