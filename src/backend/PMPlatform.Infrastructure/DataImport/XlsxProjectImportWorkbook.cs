using System.Globalization;
using System.IO.Compression;
using System.Text;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Features.Project.Import;
using PMPlatform.Infrastructure.Reports.Rendering;
using static PMPlatform.Infrastructure.Spreadsheets.SpreadsheetXml;

namespace PMPlatform.Infrastructure.DataImport;

/// <summary>
/// RPMO's import workbook (R3-4), read as the migration workbook is (<see cref="XlsxImportWorkbook"/>: untrusted input, capped, no formula
/// evaluated) and written in the shape RPMO's own file has: a title row, a row of Arabic headers with the required ones marked *, the row of
/// field keys the importer reads, then the rows to fill, each list column a drop-down over the القوائم sheet. The version stamp sits in a
/// hidden column of the key row.
/// </summary>
internal sealed class XlsxProjectImportWorkbook : IProjectImportWorkbook
{
    private const string ListsSheet = "القوائم";

    // Cell styles: default, title, header, required header, key.
    private const int StyleTitle = 1;
    private const int StyleHeader = 2;
    private const int StyleRequired = 3;
    private const int StyleKey = 4;

    private const int TitleRow = 1;
    private const int HeaderRow = 3;
    private const int KeyRow = 4;

    public ImportSheet? Read(Stream content, int rowLimit) => XlsxImportWorkbook.Read(content, rowLimit, ProjectImportLayout.DataSheet);

    public byte[] WriteTemplate(ProjectImportTemplate layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        IReadOnlyList<ProjectImportTemplateColumn> columns = layout.Columns;
        List<string> listKeys = [.. columns.Select(c => c.Key).Where(k => layout.Lists.TryGetValue(k, out IReadOnlyList<string>? values) && values.Count > 0)];
        int lastRow = KeyRow + layout.Rows;

        StringBuilder data = new($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="{Main}"><sheetViews><sheetView rightToLeft="1" workbookViewId="0"><pane ySplit="{KeyRow}" topLeftCell="A{KeyRow + 1}" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews><cols>""");
        for (int c = 0; c < columns.Count; c++)
        {
            data.Append(CultureInfo.InvariantCulture, $"""<col min="{c + 1}" max="{c + 1}" width="{Math.Clamp(columns[c].HeaderAr.Length + 6, 14, 40)}" customWidth="1"/>""");
        }

        data.Append(CultureInfo.InvariantCulture, $"""<col min="{columns.Count + 1}" max="{columns.Count + 1}" width="4" hidden="1" customWidth="1"/></cols><sheetData>""");
        data.Append(CultureInfo.InvariantCulture, $"""<row r="{TitleRow}">{Cell(0, TitleRow, "ملف استيراد المشاريع — اختر القيم من القوائم المنسدلة. الأعمدة المعلّمة بـ * إلزامية. لا تغيّر صف مفاتيح الحقول.", StyleTitle)}</row>""");
        data.Append(CultureInfo.InvariantCulture, $"""<row r="{HeaderRow}">""");
        for (int c = 0; c < columns.Count; c++)
        {
            data.Append(Cell(c, HeaderRow, columns[c].Required ? $"{columns[c].HeaderAr} *" : columns[c].HeaderAr, columns[c].Required ? StyleRequired : StyleHeader));
        }

        data.Append(CultureInfo.InvariantCulture, $"""</row><row r="{KeyRow}">""");
        for (int c = 0; c < columns.Count; c++)
        {
            data.Append(Cell(c, KeyRow, columns[c].Key, StyleKey));
        }

        data.Append(Cell(columns.Count, KeyRow, ProjectImportLayout.VersionKeyPrefix + layout.Version.ToString(CultureInfo.InvariantCulture), StyleKey));
        data.Append("</row></sheetData>");

        List<string> validations = [];
        for (int c = 0; c < columns.Count; c++)
        {
            int list = listKeys.IndexOf(columns[c].Key);
            if (list >= 0)
            {
                string source = $"'{ListsSheet}'!${ColumnName(list)}$2:${ColumnName(list)}${layout.Lists[columns[c].Key].Count + 1}";
                validations.Add($"""<dataValidation type="list" allowBlank="1" showErrorMessage="1" sqref="{ColumnName(c)}{KeyRow + 1}:{ColumnName(c)}{lastRow}"><formula1>{Escape(source)}</formula1></dataValidation>""");
            }
        }

        if (validations.Count > 0)
        {
            data.Append(CultureInfo.InvariantCulture, $"""<dataValidations count="{validations.Count}">{string.Concat(validations)}</dataValidations>""");
        }

        data.Append("</worksheet>");

        StringBuilder lists = new($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="{Main}"><sheetViews><sheetView rightToLeft="1" workbookViewId="0"/></sheetViews><sheetData>""");
        int longest = listKeys.Count == 0 ? 0 : listKeys.Max(k => layout.Lists[k].Count);
        for (int r = 0; r <= longest; r++)
        {
            lists.Append(CultureInfo.InvariantCulture, $"""<row r="{r + 1}">""");
            for (int l = 0; l < listKeys.Count; l++)
            {
                IReadOnlyList<string> values = layout.Lists[listKeys[l]];
                if (r == 0)
                {
                    lists.Append(Cell(l, 1, columns.First(c => c.Key == listKeys[l]).HeaderAr, StyleHeader));
                }
                else if (r - 1 < values.Count)
                {
                    lists.Append(Cell(l, r + 1, values[r - 1], 0));
                }
            }

            lists.Append("</row>");
        }

        lists.Append("</sheetData></worksheet>");

        using MemoryStream buffer = new();
        using (ZipArchive zip = new(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Part(zip, "[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            Part(zip, "_rels/.rels", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{PackageRelationships}"><Relationship Id="rId1" Type="{Relationships}/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Part(zip, "xl/workbook.xml", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="{Main}" xmlns:r="{Relationships}"><bookViews><workbookView/></bookViews><sheets><sheet name="{Escape(ProjectImportLayout.DataSheet)}" sheetId="1" r:id="rId1"/><sheet name="{Escape(ListsSheet)}" sheetId="2" r:id="rId2"/></sheets></workbook>""");
            Part(zip, "xl/_rels/workbook.xml.rels", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{PackageRelationships}"><Relationship Id="rId1" Type="{Relationships}/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="{Relationships}/worksheet" Target="worksheets/sheet2.xml"/><Relationship Id="rId3" Type="{Relationships}/styles" Target="styles.xml"/></Relationships>""");
            Part(zip, "xl/styles.xml", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="{Main}"><fonts count="3"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font><font><sz val="9"/><color rgb="FF808080"/><name val="Calibri"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFFE699"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="5"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="49" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1" applyNumberFormat="1"/><xf numFmtId="49" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyNumberFormat="1"/><xf numFmtId="49" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1" applyNumberFormat="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""");
            Part(zip, "xl/worksheets/sheet1.xml", data.ToString());
            Part(zip, "xl/worksheets/sheet2.xml", lists.ToString());
        }

        return buffer.ToArray();
    }

    private static string Cell(int column, int row, string text, int style)
    {
        string value = SpreadsheetSafety.Neutralize(text, out _);
        string styled = style == 0 ? string.Empty : $" s=\"{style.ToString(CultureInfo.InvariantCulture)}\"";
        return $"""<c r="{ColumnName(column)}{row.ToString(CultureInfo.InvariantCulture)}"{styled} t="inlineStr"><is><t xml:space="preserve">{Escape(value)}</t></is></c>""";
    }
}
