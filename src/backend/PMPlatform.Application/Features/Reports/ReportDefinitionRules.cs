using System.Text.RegularExpressions;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// ADM-037's validation of a version's content (FG-02 §11.2: projection, fields, filters, audience and dependencies; RPT-CC-03): its primary
/// projection and every column name FG-01's register, of the row's grain; parameters are named once, PROJECT and DEPARTMENT at most once each, and
/// an OPTION parameter filters one of the version's own coded columns with values of that column; options name only the catalogue entries the report
/// absorbs (ADR-006); the audience is named once each and only with roles the report may be offered to (BR-RPT-011, ADR-013); a report about one
/// project requires its project. Bilingual labels are complete by construction (ADR-012). Each refusal is one <c>errors[]</c> item.
/// </summary>
internal static partial class ReportDefinitionRules
{
    public static IReadOnlyList<FieldIssue> Check(ReportCode code, ReportDefinitionContent content, ReportFields fields)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fields);
        List<FieldIssue> issues = [];

        if (content.AudienceRoleCodes.Count == 0)
        {
            issues.Add(new FieldIssue("audienceRoleCodes", FieldIssue.Required));
        }

        HashSet<string> roles = new(StringComparer.Ordinal);
        for (int i = 0; i < content.AudienceRoleCodes.Count; i++)
        {
            string role = content.AudienceRoleCodes[i];
            if (!roles.Add(role))
            {
                issues.Add(new FieldIssue($"audienceRoleCodes[{i}]", FieldIssue.Duplicate));
            }
            else if (!ReportCatalogue.MayBeOfferedTo(code, role))
            {
                issues.Add(new FieldIssue($"audienceRoleCodes[{i}]", ReportIssueCodes.AudienceNotPermitted));
            }
        }

        ProjectionDescriptor? basis = fields.Projection(content.PrimaryProjectionCode);
        if (basis is null)
        {
            issues.Add(new FieldIssue("primaryProjectionCode", ReportIssueCodes.ProjectionNotRegistered));
        }

        if (content.Columns.Count == 0)
        {
            issues.Add(new FieldIssue("columns", FieldIssue.Required));
        }
        else if (!content.Columns.Any(c => c.IsDefaultVisible))
        {
            issues.Add(new FieldIssue("columns", ReportIssueCodes.ValueInvalid));
        }

        HashSet<string> columns = new(StringComparer.Ordinal);
        for (int i = 0; i < content.Columns.Count; i++)
        {
            ReportColumnInput column = content.Columns[i];
            ReportField? field = fields.Find(column.SourceEntityCode, column.FieldCode);
            if (field is null)
            {
                issues.Add(new FieldIssue($"columns[{i}]", ReportIssueCodes.ProjectionNotRegistered));
            }
            else if (!columns.Add(field.Key))
            {
                issues.Add(new FieldIssue($"columns[{i}]", FieldIssue.Duplicate));
            }
            else if (basis is not null && !field.FitsGrain(basis))
            {
                issues.Add(new FieldIssue($"columns[{i}]", ReportIssueCodes.GrainMismatch));
            }
        }

        CheckParameters(code, content, fields, columns, issues);
        return issues;
    }

    private static void CheckParameters(ReportCode code, ReportDefinitionContent content, ReportFields fields, HashSet<string> columns, List<FieldIssue> issues)
    {
        HashSet<string> codes = new(StringComparer.Ordinal);
        IReadOnlyList<string> entries = ReportCatalogue.CatalogueEntries[code];
        for (int i = 0; i < content.Parameters.Count; i++)
        {
            ReportParameterDefinitionInput parameter = content.Parameters[i];
            string at = $"parameters[{i}]";
            if (!CodeShape().IsMatch(parameter.Code) || parameter.Code.Length > 50)
            {
                issues.Add(new FieldIssue($"{at}.code", ReportIssueCodes.ValueInvalid));
            }
            else if (!codes.Add(parameter.Code))
            {
                issues.Add(new FieldIssue($"{at}.code", FieldIssue.Duplicate));
            }

            if (parameter.DataType != ReportParameterDataType.Option)
            {
                if (content.Parameters.Take(i).Any(p => p.DataType == parameter.DataType))
                {
                    issues.Add(new FieldIssue($"{at}.dataType", FieldIssue.Duplicate));
                }

                if (parameter.SourceEntityCode is not null || parameter.FieldCode is not null || parameter.Options.Count > 0)
                {
                    issues.Add(new FieldIssue($"{at}.options", FieldIssue.NotAllowed));
                }

                continue;
            }

            ReportField? field = parameter.SourceEntityCode is { } entity && parameter.FieldCode is { } fieldCode ? fields.Find(entity, fieldCode) : null;
            if (field is null || !columns.Contains(field.Key))
            {
                issues.Add(new FieldIssue($"{at}.fieldCode", ReportIssueCodes.FieldNotInReport));
                continue;
            }

            if (field.Type != ReportValueType.Code)
            {
                issues.Add(new FieldIssue($"{at}.fieldCode", ReportIssueCodes.OperatorNotSupported));
            }

            if (parameter.Options.Count == 0)
            {
                issues.Add(new FieldIssue($"{at}.options", FieldIssue.Required));
            }

            for (int o = 0; o < parameter.Options.Count; o++)
            {
                ReportParameterOptionInput option = parameter.Options[o];
                if (!field.Values.Contains(option.ValueCode, StringComparer.Ordinal))
                {
                    issues.Add(new FieldIssue($"{at}.options[{o}].valueCode", ReportIssueCodes.ValueInvalid));
                }
                else if (parameter.Options.Take(o).Any(other => other.ValueCode == option.ValueCode))
                {
                    issues.Add(new FieldIssue($"{at}.options[{o}].valueCode", FieldIssue.Duplicate));
                }

                if (option.CatalogueEntryReference is { } reference && !entries.Contains(reference, StringComparer.Ordinal))
                {
                    issues.Add(new FieldIssue($"{at}.options[{o}].catalogueEntryReference", FieldIssue.NotAllowed));
                }
            }
        }

        if (ReportCatalogue.RequiresProject(code) && !content.Parameters.Any(p => p.DataType == ReportParameterDataType.Project && p.IsRequired))
        {
            issues.Add(new FieldIssue("parameters", FieldIssue.Required));
        }
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeShape();
}
