using System.IO.Compression;
using System.Text;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Infrastructure.DataImport;

namespace PMPlatform.Tests.Unit.Infrastructure.DataImport;

/// <summary>
/// TASK-102: the migration workbook is read as a spreadsheet saves it — shared strings, inline strings, numbers, booleans, phonetic runs —
/// and an upload that is not a workbook, or is built to exhaust the reader, is refused rather than read.
/// </summary>
public sealed class XlsxImportWorkbookTests
{
    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly ImportColumn[] Columns =
    [
        ImportColumn.LegacyId,
        new("code", ImportValueKind.Code, Required: true, "Code", "الرمز"),
        new("status", ImportValueKind.Choice, Required: false, "Status", "الحالة") { Choices = ["ACTIVE", "CLOSED"] },
    ];

    private readonly XlsxImportWorkbook _workbook = new();

    [Fact]
    public void TheTemplateReadsBackAsItsHeaderAndListsItsColumnsAndReferenceValues()
    {
        byte[] template = _workbook.WriteTemplate(Columns, new Dictionary<string, IReadOnlyList<ImportReferenceValue>>
        {
            ["code"] = [new ImportReferenceValue("=HYPERLINK()", "قيمة", "Value")],
        });

        ImportSheet sheet = Assert.IsType<ImportSheet>(_workbook.Read(new MemoryStream(template), rowLimit: 10));

        ImportSheetRow header = Assert.Single(sheet.Rows);
        Assert.Equal(["legacy_id", "code", "status"], header.Cells.OrderBy(c => c.Key).Select(c => c.Value));
        string data = Part(template, "xl/worksheets/sheet1.xml");
        Assert.Contains("""<formula1>"ACTIVE,CLOSED"</formula1>""", data, StringComparison.Ordinal);
        Assert.Contains("""sqref="C2:C5001""", data, StringComparison.Ordinal);
        Assert.Contains("الرمز", Part(template, "xl/worksheets/sheet2.xml"), StringComparison.Ordinal);
        string references = Part(template, "xl/worksheets/sheet3.xml");
        Assert.Contains("قيمة", references, StringComparison.Ordinal);
        Assert.Contains("""s="5" t="inlineStr"><is><t xml:space="preserve">=HYPERLINK()</t>""", references, StringComparison.Ordinal);
    }

    [Fact]
    public void ATemplateWithNoReferenceValuesHasNoReferenceSheet()
    {
        byte[] template = _workbook.WriteTemplate(Columns, new Dictionary<string, IReadOnlyList<ImportReferenceValue>>());

        using ZipArchive zip = new(new MemoryStream(template));
        Assert.Null(zip.GetEntry("xl/worksheets/sheet3.xml"));
        Assert.DoesNotContain("Reference values", Part(template, "xl/workbook.xml"), StringComparison.Ordinal);
    }

    [Fact]
    public void CellsAreReadAsTheSpreadsheetShowsThem()
    {
        byte[] file = Workbook(
            sheet: """
                <row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="inlineStr"><is><t>code</t></is></c></row>
                <row r="3"><c r="A3" t="s"><v>1</v></c><c r="B3" t="b"><v>1</v></c><c r="C3"><v>45658</v></c><c r="E3" t="str"><f>A3</f><v>formula result</v></c></row>
                <row r="4"><c r="A4" t="s"><v>2</v></c><c r="B4"/></row>
                <row r="5"><c r="A5" t="inlineStr"><is><t>   </t></is></c></row>
                """,
            sharedStrings: """<si><t>legacy_id</t></si><si><r><t>LEG</t></r><r><t>-1</t></r></si><si><t>漢字</t><rPh sb="0" eb="2"><t>カンジ</t></rPh></si>""");

        ImportSheet sheet = Assert.IsType<ImportSheet>(_workbook.Read(new MemoryStream(file), rowLimit: 10));

        Assert.Equal([1, 3, 4], sheet.Rows.Select(r => r.RowNumber));
        Assert.Equal(new Dictionary<int, string> { [0] = "LEG-1", [1] = "TRUE", [2] = "45658", [4] = "formula result" }, sheet.Rows[1].Cells);
        Assert.Equal("漢字", Assert.Single(sheet.Rows[2].Cells).Value);
    }

    [Fact]
    public void TheDataSheetIsReadWhereverItIs()
    {
        byte[] file = Workbook(
            sheet: """<row r="1"><c r="A1" t="inlineStr"><is><t>notes</t></is></c></row>""",
            secondSheet: """<row r="1"><c r="A1" t="inlineStr"><is><t>legacy_id</t></is></c></row>""");

        ImportSheet sheet = Assert.IsType<ImportSheet>(_workbook.Read(new MemoryStream(file), rowLimit: 10));

        Assert.Equal("legacy_id", sheet.Rows[0].Cells[0]);
    }

    [Fact]
    public void ReadingStopsAtTheRowLimit()
    {
        string rows = string.Concat(Enumerable.Range(1, 50).Select(r => $"""<row r="{r}"><c r="A{r}"><v>{r}</v></c></row>"""));

        ImportSheet sheet = Assert.IsType<ImportSheet>(_workbook.Read(new MemoryStream(Workbook(rows)), rowLimit: 7));

        Assert.Equal(7, sheet.Rows.Count);
    }

    [Fact]
    public void AFileThatIsNotAWorkbookIsRefused()
    {
        Assert.Null(_workbook.Read(new MemoryStream(Encoding.UTF8.GetBytes("legacy_id,code\nL1,A\n")), rowLimit: 10));
        Assert.Null(_workbook.Read(new MemoryStream(Zip(("readme.txt", "hello"))), rowLimit: 10));
    }

    [Fact]
    public void AWorkbookCarryingADocumentTypeDefinitionIsRefused()
    {
        string entity = """<?xml version="1.0"?><!DOCTYPE worksheet [<!ENTITY a "aaaaaaaaaa"><!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;">]>""";
        byte[] file = Workbook($"""<row r="1"><c r="A1" t="inlineStr"><is><t>&b;</t></is></c></row>""", sheetPrologue: entity);

        Assert.Null(_workbook.Read(new MemoryStream(file), rowLimit: 10));
    }

    [Fact]
    public void APartThatDecompressesPastTheCapIsRefused()
    {
        // 70 MB of one repeated cell compresses to a few hundred kilobytes: the shape of a decompression bomb.
        StringBuilder sheet = new();
        string row = """<row><c t="inlineStr"><is><t>xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx</t></is></c></row>""";
        while (sheet.Length < 70 * 1024 * 1024)
        {
            sheet.Append(row);
        }

        Assert.Null(_workbook.Read(new MemoryStream(Workbook(sheet.ToString())), rowLimit: int.MaxValue));
    }

    private static string Part(byte[] workbook, string name)
    {
        using ZipArchive zip = new(new MemoryStream(workbook));
        using StreamReader reader = new(zip.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    /// <summary>A minimal workbook as a spreadsheet application writes one: Data first, unless a second sheet is given, which is then named Data.</summary>
    private static byte[] Workbook(string sheet, string? sharedStrings = null, string? secondSheet = null, string sheetPrologue = """<?xml version="1.0" encoding="UTF-8"?>""")
    {
        string sheets = secondSheet is null
            ? """<sheet name="Data" sheetId="1" r:id="rId1"/>"""
            : """<sheet name="Notes" sheetId="1" r:id="rId1"/><sheet name="Data" sheetId="2" r:id="rId2"/>""";
        List<(string, string)> parts =
        [
            ("_rels/.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>"""),
            ("xl/workbook.xml", $"""<workbook xmlns="{Main}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>{sheets}</sheets></workbook>"""),
            ("xl/_rels/workbook.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="/xl/worksheets/sheet2.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/></Relationships>"""),
            ("xl/worksheets/sheet1.xml", $"""{sheetPrologue}<worksheet xmlns="{Main}"><sheetData>{sheet}</sheetData></worksheet>"""),
        ];
        if (secondSheet is not null)
        {
            parts.Add(("xl/worksheets/sheet2.xml", $"""<worksheet xmlns="{Main}"><sheetData>{secondSheet}</sheetData></worksheet>"""));
        }

        if (sharedStrings is not null)
        {
            parts.Add(("xl/sharedStrings.xml", $"""<sst xmlns="{Main}">{sharedStrings}</sst>"""));
        }

        return Zip([.. parts]);
    }

    private static byte[] Zip(params (string Name, string Content)[] parts)
    {
        using MemoryStream buffer = new();
        using (ZipArchive zip = new(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in parts)
            {
                using Stream stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
                stream.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return buffer.ToArray();
    }
}
