namespace PMPlatform.Application.Common.DataImport;

/// <summary>The XLSX side of an import: the template a register hands out, and the rows of a workbook handed back.</summary>
public interface IImportWorkbook
{
    /// <summary>
    /// The cells of the workbook's Data sheet (its first sheet when none is named so), as text, stopping once it holds
    /// <paramref name="rowLimit"/> rows; null when it is not a workbook that can be read.
    /// </summary>
    public ImportSheet? Read(Stream content, int rowLimit);

    /// <summary>A workbook with the register's headers on its Data sheet, the Columns sheet describing each, and the Reference values sheet.</summary>
    public byte[] WriteTemplate(IReadOnlyList<ImportColumn> columns, IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>> referenceValues);
}

/// <summary>A worksheet as text: the first row is the header. Rows with no value at all are left out.</summary>
public sealed record ImportSheet(IReadOnlyList<ImportSheetRow> Rows);

/// <summary>A row's non-empty cells by zero-based column index, as the cell displays them: a number in invariant form, a boolean as TRUE or FALSE.</summary>
public sealed record ImportSheetRow(int RowNumber, IReadOnlyDictionary<int, string> Cells);
