using System.Text.Json;
using System.Text.RegularExpressions;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Api.Models.Reports;

/// <summary>A report named in a route: one of the ten of ADR-006 by its R-19 code; any other name is no report (404).</summary>
internal static class ReportRoute
{
    public static ReportCode? Parse(string value) =>
        Enum.GetValues<ReportCode>().Cast<ReportCode?>().FirstOrDefault(c => JsonNamingPolicy.SnakeCaseUpper.ConvertName(c!.Value.ToString()) == value);
}

/// <summary>
/// Shape validation of report requests (T-3: shape here, meaning in the module). A field is named by two codes and a value is text: nothing a
/// request carries is a query, an expression or a formula (BR-RPT-046). Whether a code names an allowlisted or published field is the module's 422.
/// </summary>
internal static partial class ReportRequestValidation
{
    public const int EntityCodeLength = 100;
    public const int MaxColumns = 100;
    public const int MaxFilters = 20;
    public const int MaxSorts = 10;
    public const int MaxParameters = 20;
    public const int ValueLength = 500;

    public static void Field(string? sourceEntityCode, string? fieldCode, string at, List<FieldError> errors)
    {
        Shape(sourceEntityCode, $"{at}.sourceEntityCode", EntityCodeLength, errors);
        Shape(fieldCode, $"{at}.fieldCode", EntityCodeLength, errors);
    }

    public static void Shape(string? value, string field, int maxLength, List<FieldError> errors)
    {
        RequestValidation.Require(value, field, maxLength, errors);
        if (!string.IsNullOrEmpty(value) && value.Length <= maxLength && !CodeShape().IsMatch(value))
        {
            errors.Add(new FieldError(field, FieldError.Malformed));
        }
    }

    public static IReadOnlyList<ReportParameterInput> Parameters(IReadOnlyList<ReportParameterRequest?>? parameters, List<FieldError> errors)
    {
        if (parameters is null)
        {
            return [];
        }

        if (parameters.Count > MaxParameters)
        {
            errors.Add(new FieldError("parameters", FieldError.OutOfRange));
            return [];
        }

        for (int i = 0; i < parameters.Count; i++)
        {
            Shape(parameters[i]?.Code, $"parameters[{i}].code", RequestValidation.CodeLength, errors);
            RequestValidation.Require(parameters[i]?.Value, $"parameters[{i}].value", ValueLength, errors);
        }

        return [.. parameters.Select(p => new ReportParameterInput(p?.Code ?? string.Empty, p?.Value ?? string.Empty))];
    }

    public static IReadOnlyList<ReportFieldReference>? Columns(IReadOnlyList<ReportFieldRequest?>? columns, bool required, List<FieldError> errors)
    {
        if (columns is null)
        {
            if (required)
            {
                errors.Add(new FieldError("columns", FieldError.Required));
            }

            return null;
        }

        if (columns.Count > MaxColumns)
        {
            errors.Add(new FieldError("columns", FieldError.OutOfRange));
            return [];
        }

        for (int i = 0; i < columns.Count; i++)
        {
            Field(columns[i]?.SourceEntityCode, columns[i]?.FieldCode, $"columns[{i}]", errors);
        }

        return [.. columns.Select(c => new ReportFieldReference(c?.SourceEntityCode ?? string.Empty, c?.FieldCode ?? string.Empty))];
    }

    public static IReadOnlyList<ReportSortInput> Sort(IReadOnlyList<ReportSortRequest?>? sort, List<FieldError> errors)
    {
        if (sort is null)
        {
            return [];
        }

        if (sort.Count > MaxSorts)
        {
            errors.Add(new FieldError("sort", FieldError.OutOfRange));
            return [];
        }

        for (int i = 0; i < sort.Count; i++)
        {
            Field(sort[i]?.SourceEntityCode, sort[i]?.FieldCode, $"sort[{i}]", errors);
            if (sort[i]?.Direction is null)
            {
                errors.Add(new FieldError($"sort[{i}].direction", FieldError.Required));
            }
        }

        return [.. sort.Select(s => new ReportSortInput(s?.SourceEntityCode ?? string.Empty, s?.FieldCode ?? string.Empty, s?.Direction ?? ReportSortDirection.Asc))];
    }

    public static IReadOnlyList<ReportFilterInput> Filters(IReadOnlyList<ReportFilterRequest?>? filters, List<FieldError> errors)
    {
        if (filters is null)
        {
            return [];
        }

        if (filters.Count > MaxFilters)
        {
            errors.Add(new FieldError("filters", FieldError.OutOfRange));
            return [];
        }

        for (int i = 0; i < filters.Count; i++)
        {
            Field(filters[i]?.SourceEntityCode, filters[i]?.FieldCode, $"filters[{i}]", errors);
            if (filters[i]?.Operator is null)
            {
                errors.Add(new FieldError($"filters[{i}].operator", FieldError.Required));
            }

            RequestValidation.Require(filters[i]?.Value, $"filters[{i}].value", ValueLength, errors);
        }

        return [.. filters.Select(f => new ReportFilterInput(f?.SourceEntityCode ?? string.Empty, f?.FieldCode ?? string.Empty, f?.Operator ?? ReportFilterOperator.Eq, f?.Value ?? string.Empty))];
    }

    public static ReportExportInput Export(ReportExportFormat? format, string? language, List<FieldError> errors)
    {
        if (format is null)
        {
            errors.Add(new FieldError("format", FieldError.Required));
        }

        Language lang = RequestValidation.Language(language, "language", fallback: null, errors);
        return new ReportExportInput(format ?? ReportExportFormat.Pdf, lang);
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeShape();
}

public sealed record ReportParameterRequest(string? Code, string? Value);

/// <summary>A field by the names configuration gives it: <c>PROJECT</c> or a projection's code without its dot, and the field's code.</summary>
public sealed record ReportFieldRequest(string? SourceEntityCode, string? FieldCode);

public sealed record ReportSortRequest(string? SourceEntityCode, string? FieldCode, ReportSortDirection? Direction);

/// <summary>A filter: a field, an operator of the closed set and a value (for IN several, for BETWEEN two, separated by commas). Never an expression.</summary>
public sealed record ReportFilterRequest(string? SourceEntityCode, string? FieldCode, ReportFilterOperator? Operator, string? Value);

/// <summary>A run of a published report: its parameters, a choice among its columns (its default columns when absent), and an order.</summary>
public sealed record ReportRunRequest(IReadOnlyList<ReportParameterRequest?>? Parameters, IReadOnlyList<ReportFieldRequest?>? Columns, IReadOnlyList<ReportSortRequest?>? Sort)
{
    internal List<FieldError> Validate(out ReportRunInput input)
    {
        List<FieldError> errors = [];
        input = new ReportRunInput(
            ReportRequestValidation.Parameters(Parameters, errors), ReportRequestValidation.Columns(Columns, required: false, errors), ReportRequestValidation.Sort(Sort, errors));
        return errors;
    }
}

/// <summary>MOD-060: a report run as a PDF, XLSX or CSV (ADR-005), in Arabic or English.</summary>
public sealed record ReportExportRequest(
    IReadOnlyList<ReportParameterRequest?>? Parameters,
    IReadOnlyList<ReportFieldRequest?>? Columns,
    IReadOnlyList<ReportSortRequest?>? Sort,
    ReportExportFormat? Format,
    string? Language)
{
    internal List<FieldError> Validate(out ReportRunInput input, out ReportExportInput output)
    {
        List<FieldError> errors = new ReportRunRequest(Parameters, Columns, Sort).Validate(out input);
        output = ReportRequestValidation.Export(Format, Language, errors);
        return errors;
    }
}

/// <summary>An SCR-138 composition: allowlisted columns, filters and sorts only.</summary>
public sealed record ExplorerRunRequest(IReadOnlyList<ReportFieldRequest?>? Columns, IReadOnlyList<ReportFilterRequest?>? Filters, IReadOnlyList<ReportSortRequest?>? Sort)
{
    internal List<FieldError> Validate(out ExplorerRunInput input)
    {
        List<FieldError> errors = [];
        input = new ExplorerRunInput(
            ReportRequestValidation.Columns(Columns, required: true, errors) ?? [], ReportRequestValidation.Filters(Filters, errors), ReportRequestValidation.Sort(Sort, errors));
        return errors;
    }
}

/// <summary>An SCR-138 composition as a PDF, XLSX or CSV.</summary>
public sealed record ExplorerExportRequest(
    IReadOnlyList<ReportFieldRequest?>? Columns,
    IReadOnlyList<ReportFilterRequest?>? Filters,
    IReadOnlyList<ReportSortRequest?>? Sort,
    ReportExportFormat? Format,
    string? Language)
{
    internal List<FieldError> Validate(out ExplorerRunInput input, out ReportExportInput output)
    {
        List<FieldError> errors = new ExplorerRunRequest(Columns, Filters, Sort).Validate(out input);
        output = ReportRequestValidation.Export(Format, Language, errors);
        return errors;
    }
}

public sealed record SavedViewColumnRequest(string? SourceEntityCode, string? FieldCode, ReportSortDirection? SortDirection);

/// <summary>
/// MOD-062: a private saved view, whole — a report's parameter values (REPORT_PARAMETERS), or an SCR-138 composition of allowlisted columns, with
/// their sorts, and filters (EXPLORER_COMPOSITION). Configuration only.
/// </summary>
public sealed record SavedViewRequest(
    NarrativeTextRequest? Name,
    SavedViewType? ViewType,
    ReportCode? ReportCode,
    IReadOnlyList<ReportParameterRequest?>? Parameters,
    IReadOnlyList<SavedViewColumnRequest?>? Columns,
    IReadOnlyList<ReportFilterRequest?>? Filters)
{
    internal List<FieldError> Validate(out SavedViewInput? input)
    {
        List<FieldError> errors = [];
        NarrativeText? name = Name?.Validate("name", errors);
        if (Name is null)
        {
            errors.Add(new FieldError("name", FieldError.Required));
        }

        if (ViewType is null)
        {
            errors.Add(new FieldError("viewType", FieldError.Required));
        }

        IReadOnlyList<ReportParameterInput> parameters = ReportRequestValidation.Parameters(Parameters, errors);
        IReadOnlyList<ReportFieldReference> columns = ReportRequestValidation.Columns(
            Columns?.Select(c => c is null ? null : new ReportFieldRequest(c.SourceEntityCode, c.FieldCode)).ToList(), required: false, errors) ?? [];
        IReadOnlyList<ReportFilterInput> filters = ReportRequestValidation.Filters(Filters, errors);
        input = errors.Count == 0
            ? new SavedViewInput(
                name!, ViewType!.Value, ReportCode, parameters,
                [.. columns.Select((c, i) => new SavedViewColumnInput(c.SourceEntityCode, c.FieldCode, Columns![i]!.SortDirection))], filters)
            : null;
        return errors;
    }
}

/// <summary>A new version of one of the ten reports; it opens as a DRAFT copy of the PUBLISHED version.</summary>
public sealed record ReportDefinitionCreateRequest(ReportCode? Code)
{
    internal List<FieldError> Validate(out ReportCode code)
    {
        List<FieldError> errors = [];
        if (Code is null)
        {
            errors.Add(new FieldError("code", FieldError.Required));
        }

        code = Code ?? default;
        return errors;
    }
}

public sealed record ReportColumnRequest(string? SourceEntityCode, string? FieldCode, BilingualLabelRequest? Label, bool? IsDefaultVisible, Guid? DataClassificationItemId);

public sealed record ReportParameterOptionRequest(string? ValueCode, BilingualLabelRequest? Label, string? CatalogueEntryReference);

public sealed record ReportParameterDefinitionRequest(
    string? Code,
    BilingualLabelRequest? Label,
    ReportParameterDataType? DataType,
    bool? IsRequired,
    string? SourceEntityCode,
    string? FieldCode,
    IReadOnlyList<ReportParameterOptionRequest?>? Options);

/// <summary>
/// A DRAFT version's content, as a whole (R-5): bilingual labels (ADR-012), audience family, the projection that decides its rows, saved views, the
/// roles that may run it, its parameters and columns. Every one names a registered projection or field by code (BR-RPT-046); whether it is
/// registered, of the row's grain and offered to the right roles is the module's validation.
/// </summary>
public sealed record ReportDefinitionRequest(
    BilingualLabelRequest? Name,
    BilingualLabelRequest? Description,
    ReportAudienceFamily? AudienceFamily,
    string? PrimaryProjectionCode,
    bool? AllowsSavedViews,
    IReadOnlyList<string?>? AudienceRoleCodes,
    IReadOnlyList<ReportParameterDefinitionRequest?>? Parameters,
    IReadOnlyList<ReportColumnRequest?>? Columns)
{
    internal List<FieldError> Validate(out ReportDefinitionContent? content)
    {
        List<FieldError> errors = [];
        RequestValidation.Label(Name, "name", errors);
        if (Description is not null)
        {
            RequestValidation.Label(Description, "description", errors);
        }

        if (AudienceFamily is null)
        {
            errors.Add(new FieldError("audienceFamily", FieldError.Required));
        }

        RequestValidation.Require(PrimaryProjectionCode, "primaryProjectionCode", ReportRequestValidation.EntityCodeLength, errors);
        if (AllowsSavedViews is null)
        {
            errors.Add(new FieldError("allowsSavedViews", FieldError.Required));
        }

        if (AudienceRoleCodes is null)
        {
            errors.Add(new FieldError("audienceRoleCodes", FieldError.Required));
        }
        else
        {
            for (int i = 0; i < AudienceRoleCodes.Count; i++)
            {
                RequestValidation.Code(AudienceRoleCodes[i], $"audienceRoleCodes[{i}]", errors);
            }
        }

        ValidateParameters(errors);
        ValidateColumns(errors);
        content = errors.Count == 0
            ? new ReportDefinitionContent(
                Name!.ToLabel(),
                Description?.ToLabel(),
                AudienceFamily!.Value,
                PrimaryProjectionCode!,
                AllowsSavedViews!.Value,
                [.. AudienceRoleCodes!.Select(r => r!)],
                [.. (Parameters ?? []).Select(p => new ReportParameterDefinitionInput(
                    p!.Code!, p.Label!.ToLabel(), p.DataType!.Value, p.IsRequired ?? false, p.SourceEntityCode, p.FieldCode,
                    [.. (p.Options ?? []).Select(o => new ReportParameterOptionInput(o!.ValueCode!, o.Label!.ToLabel(), o.CatalogueEntryReference))]))],
                [.. Columns!.Select(c => new ReportColumnInput(c!.SourceEntityCode!, c.FieldCode!, c.Label!.ToLabel(), c.IsDefaultVisible ?? false, c.DataClassificationItemId))])
            : null;
        return errors;
    }

    private void ValidateParameters(List<FieldError> errors)
    {
        if (Parameters is null)
        {
            return;
        }

        if (Parameters.Count > ReportRequestValidation.MaxParameters)
        {
            errors.Add(new FieldError("parameters", FieldError.OutOfRange));
            return;
        }

        for (int i = 0; i < Parameters.Count; i++)
        {
            string at = $"parameters[{i}]";
            ReportParameterDefinitionRequest? p = Parameters[i];
            if (p is null)
            {
                errors.Add(new FieldError(at, FieldError.Required));
                continue;
            }

            ReportRequestValidation.Shape(p.Code, $"{at}.code", RequestValidation.CodeLength, errors);
            RequestValidation.Label(p.Label, $"{at}.label", errors);
            if (p.DataType is null)
            {
                errors.Add(new FieldError($"{at}.dataType", FieldError.Required));
            }

            if (p.SourceEntityCode is not null || p.FieldCode is not null)
            {
                ReportRequestValidation.Field(p.SourceEntityCode, p.FieldCode, at, errors);
            }

            for (int o = 0; o < (p.Options?.Count ?? 0); o++)
            {
                ReportParameterOptionRequest? option = p.Options![o];
                ReportRequestValidation.Shape(option?.ValueCode, $"{at}.options[{o}].valueCode", RequestValidation.CodeLength, errors);
                RequestValidation.Label(option?.Label, $"{at}.options[{o}].label", errors);
                if (option?.CatalogueEntryReference is { Length: > RequestValidation.CodeLength })
                {
                    errors.Add(new FieldError($"{at}.options[{o}].catalogueEntryReference", FieldError.MaxLength));
                }
            }
        }
    }

    private void ValidateColumns(List<FieldError> errors)
    {
        if (Columns is null)
        {
            errors.Add(new FieldError("columns", FieldError.Required));
            return;
        }

        if (Columns.Count > ReportRequestValidation.MaxColumns)
        {
            errors.Add(new FieldError("columns", FieldError.OutOfRange));
            return;
        }

        for (int i = 0; i < Columns.Count; i++)
        {
            ReportColumnRequest? c = Columns[i];
            if (c is null)
            {
                errors.Add(new FieldError($"columns[{i}]", FieldError.Required));
                continue;
            }

            ReportRequestValidation.Field(c.SourceEntityCode, c.FieldCode, $"columns[{i}]", errors);
            RequestValidation.Label(c.Label, $"columns[{i}].label", errors);
            if (c.DataClassificationItemId == Guid.Empty)
            {
                errors.Add(new FieldError($"columns[{i}].dataClassificationItemId", FieldError.Malformed));
            }
        }
    }
}
