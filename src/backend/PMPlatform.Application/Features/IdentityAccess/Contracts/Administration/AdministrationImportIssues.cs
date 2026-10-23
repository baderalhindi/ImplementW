using System.Text;
using PMPlatform.Application.Common.DataImport;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// A refused administration request as the issues of a migration workbook row (TASK-102): each refused field under the column that
/// fills it, so the row's report reads as the form's errors would.
/// </summary>
public static class AdministrationImportIssues
{
    /// <summary>
    /// <paramref name="columnOf"/> names the column a request field comes from when it is not the field in snake case, e.g. a
    /// <c>parentDepartmentId</c> that the workbook gives as <c>parent_code</c>.
    /// </summary>
    public static ImportRowResult Rejected(AdministrationError error, IReadOnlyDictionary<string, string>? columnOf = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        string rowCode = error.Code ?? SnakeCase(error.Kind.ToString()).ToUpperInvariant();
        return error.Fields.Count == 0
            ? ImportRowResult.Rejected(string.Empty, rowCode)
            : ImportRowResult.Rejected([.. error.Fields.Select(f => new ImportCellIssue(columnOf?.GetValueOrDefault(f.Field) ?? SnakeCase(f.Field), f.Code))]);
    }

    /// <summary><c>name.ar</c> → <c>name_ar</c>; <c>directoryReference</c> → <c>directory_reference</c>.</summary>
    public static string SnakeCase(string field)
    {
        ArgumentNullException.ThrowIfNull(field);
        StringBuilder column = new(field.Length + 8);
        foreach (char c in field)
        {
            if (c == '.')
            {
                column.Append('_');
            }
            else if (char.IsUpper(c))
            {
                if (column.Length > 0)
                {
                    column.Append('_');
                }

                column.Append(char.ToLowerInvariant(c));
            }
            else
            {
                column.Append(c);
            }
        }

        return column.ToString();
    }
}
