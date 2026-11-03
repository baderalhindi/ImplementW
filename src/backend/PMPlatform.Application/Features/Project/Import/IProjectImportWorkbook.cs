using PMPlatform.Application.Common.DataImport;

namespace PMPlatform.Application.Features.Project.Import;

/// <summary>
/// RPMO's workbook as .xlsx (R3-4): its data sheet "بيانات المشاريع" — title rows, a row of Arabic headers, a row of field keys, then one row per
/// project — and the template the platform hands out in the same shape, with its lists from live master data.
/// </summary>
public interface IProjectImportWorkbook
{
    /// <summary>The data sheet's rows (the one named <see cref="ProjectImportLayout.DataSheet"/>, else the first); null when it is not a workbook.</summary>
    public ImportSheet? Read(Stream content, int rowLimit);

    public byte[] WriteTemplate(ProjectImportTemplate layout);
}

/// <summary>The template's columns, in order, and the lists their drop-downs offer, by column key.</summary>
public sealed record ProjectImportTemplate(
    IReadOnlyList<ProjectImportTemplateColumn> Columns, IReadOnlyDictionary<string, IReadOnlyList<string>> Lists, int Version, int Rows);

public sealed record ProjectImportTemplateColumn(string Key, string HeaderAr, bool Required);
