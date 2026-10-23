using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Infrastructure.Reports.Rendering;
using static PMPlatform.Infrastructure.Spreadsheets.SpreadsheetXml;

namespace PMPlatform.Infrastructure.DataImport;

/// <summary>
/// The migration workbook as Office Open XML (TASK-102), read and written directly like the report export (ADR-005). A workbook is
/// untrusted input: no DTD is processed and nothing is resolved, every part read is capped, reading stops at the row limit, and only
/// element local names are matched, so a transitional or a strict workbook reads the same. A formula's cached value is read; the
/// formula never is.
/// </summary>
internal sealed class XlsxImportWorkbook : IImportWorkbook
{
    public const string DataSheet = "Data";
    private const string ColumnsSheet = "Columns";
    private const string ReferenceSheet = "Reference values";

    /// <summary>The most a single part may decompress to; far above a 5,000-row sheet, far below a decompression bomb.</summary>
    private const long MaxPartBytes = 64L * 1024 * 1024;

    private const int MaxEntries = 1_000;

    /// <summary>The template's data validations cover the rows a workbook may hold, after the header.</summary>
    private const int TemplateRows = ImportLimits.MaxRows + 1;

    // Cell styles (cellXfs order in Styles): default, header, required header, text, wrapped text, quote-prefixed text.
    private const int StyleHeader = 1;
    private const int StyleRequiredHeader = 2;
    private const int StyleText = 3;
    private const int StyleWrapped = 4;
    private const int StyleQuoted = 5;

    private static readonly string[] ColumnsHeader = ["Column", "Required", "Type", "Max length", "Allowed values", "Description", "الوصف"];
    private static readonly string[] ReferenceHeader = ["Column", "Code", "الاسم", "Name"];

    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
    };

    public ImportSheet? Read(Stream content, int rowLimit)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            using ZipArchive zip = new(content, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count > MaxEntries
                || ReadRelationships(zip, "_rels/.rels", string.Empty).Values.FirstOrDefault(r => r.Type.EndsWith("/officeDocument", StringComparison.Ordinal)) is not { } document)
            {
                return null;
            }

            string workbookPath = document.Target;
            string folder = workbookPath.Contains('/', StringComparison.Ordinal) ? workbookPath[..(workbookPath.LastIndexOf('/') + 1)] : string.Empty;
            Dictionary<string, Relationship> parts = ReadRelationships(zip, $"{folder}_rels/{workbookPath[folder.Length..]}.rels", folder);
            if (SheetId(zip, workbookPath) is not { } sheetId || !parts.TryGetValue(sheetId, out Relationship? sheet))
            {
                return null;
            }

            IReadOnlyList<string> sharedStrings = parts.Values.FirstOrDefault(r => r.Type.EndsWith("/sharedStrings", StringComparison.Ordinal)) is { } strings
                ? SharedStrings(zip, strings.Target)
                : [];
            return Rows(zip, sheet.Target, sharedStrings, rowLimit);
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or FormatException or OverflowException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public byte[] WriteTemplate(IReadOnlyList<ImportColumn> columns, IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>> referenceValues)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(referenceValues);

        List<List<Cell>> data = [[.. columns.Select(c => new Cell(c.Key, c.Required ? StyleRequiredHeader : StyleHeader))]];
        List<List<Cell>> described =
        [
            [.. ColumnsHeader.Select(h => new Cell(h, StyleHeader))],
            .. columns.Select(c => new List<Cell>
            {
                Text(c.Key),
                Text(c.Required ? "Yes / نعم" : "No / لا"),
                Text(TypeOf(c.Kind)),
                Text(c.Kind is ImportValueKind.Text or ImportValueKind.Code or ImportValueKind.Email ? c.MaxLength.ToString(CultureInfo.InvariantCulture) : string.Empty),
                Text(c.Choices.Count > 0 ? string.Join(", ", c.Choices) : referenceValues.ContainsKey(c.Key) ? $"See the \"{ReferenceSheet}\" sheet" : string.Empty),
                Text(c.DescriptionEn, StyleWrapped),
                Text(c.DescriptionAr, StyleWrapped),
            }),
        ];
        List<List<Cell>> references =
        [
            [.. ReferenceHeader.Select(h => new Cell(h, StyleHeader))],
            .. referenceValues.OrderBy(r => columns.ToList().FindIndex(c => c.Key == r.Key))
                .SelectMany(r => r.Value.Select(v => new List<Cell> { Text(r.Key), Text(v.Code), Text(v.NameAr), Text(v.NameEn) })),
        ];
        string[] sheets = references.Count > 1 ? [DataSheet, ColumnsSheet, ReferenceSheet] : [DataSheet, ColumnsSheet];

        using MemoryStream buffer = new();
        using (ZipArchive zip = new(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Part(zip, "[Content_Types].xml", ContentTypes(sheets.Length));
            Part(zip, "_rels/.rels", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{PackageRelationships}"><Relationship Id="rId1" Type="{Relationships}/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Part(zip, "xl/workbook.xml", Workbook(sheets));
            Part(zip, "xl/_rels/workbook.xml.rels", WorkbookRelationships(sheets.Length));
            Part(zip, "xl/styles.xml", Styles());
            Part(zip, "xl/worksheets/sheet1.xml", Sheet(data, freezeHeader: true, bodyStyle: StyleText, Validations(columns)));
            Part(zip, "xl/worksheets/sheet2.xml", Sheet(described, freezeHeader: true, bodyStyle: 0, string.Empty));
            if (sheets.Length == 3)
            {
                Part(zip, "xl/worksheets/sheet3.xml", Sheet(references, freezeHeader: true, bodyStyle: 0, string.Empty));
            }
        }

        return buffer.ToArray();
    }

    private static string TypeOf(ImportValueKind kind) => kind switch
    {
        ImportValueKind.Text => "Text",
        ImportValueKind.Code => "Code: A–Z, 0–9, - and _",
        ImportValueKind.Email => "E-mail address",
        ImportValueKind.PhoneNumber => "Mobile number, E.164 (+9665…)",
        ImportValueKind.Date => "Date, yyyy-mm-dd",
        ImportValueKind.Number => "Number",
        ImportValueKind.WholeNumber => "Whole number",
        ImportValueKind.Boolean => "TRUE or FALSE",
        ImportValueKind.Choice => "One of the allowed values",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown import value kind."),
    };

    /// <summary>A drop-down list on each choice column whose values fit a list validation (255 characters), and on each TRUE/FALSE column.</summary>
    private static string Validations(IReadOnlyList<ImportColumn> columns)
    {
        List<string> lists = [];
        for (int i = 0; i < columns.Count; i++)
        {
            string list = columns[i].Kind == ImportValueKind.Boolean ? "TRUE,FALSE" : string.Join(",", columns[i].Choices);
            if (columns[i].Kind is ImportValueKind.Choice or ImportValueKind.Boolean && list.Length is > 0 and <= 255)
            {
                string range = $"{ColumnName(i)}2:{ColumnName(i)}{TemplateRows.ToString(CultureInfo.InvariantCulture)}";
                lists.Add($"""<dataValidation type="list" allowBlank="1" showErrorMessage="1" sqref="{range}"><formula1>"{Escape(list)}"</formula1></dataValidation>""");
            }
        }

        return lists.Count == 0 ? string.Empty : $"""<dataValidations count="{lists.Count.ToString(CultureInfo.InvariantCulture)}">{string.Concat(lists)}</dataValidations>""";
    }

    private static Cell Text(string text, int style = 0)
    {
        string value = SpreadsheetSafety.Neutralize(text, out bool neutralized);
        return neutralized ? new Cell(value[1..], StyleQuoted) : new Cell(value, style);
    }

    private static string Sheet(List<List<Cell>> rows, bool freezeHeader, int bodyStyle, string validations)
    {
        StringBuilder xml = new($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="{Main}"><sheetViews><sheetView workbookViewId="0">""");
        if (freezeHeader)
        {
            xml.Append("""<pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/>""");
        }

        xml.Append("</sheetView></sheetViews><cols>");
        int columns = rows.Max(r => r.Count);
        for (int c = 0; c < columns; c++)
        {
            int width = Math.Clamp(rows.Where(r => c < r.Count).Max(r => r[c].Value.Length) + 2, 10, 60);
            string style = bodyStyle == 0 ? string.Empty : $" style=\"{bodyStyle.ToString(CultureInfo.InvariantCulture)}\"";
            xml.Append(CultureInfo.InvariantCulture, $"""<col min="{c + 1}" max="{c + 1}" width="{width}" customWidth="1"{style}/>""");
        }

        xml.Append("</cols><sheetData>");
        for (int r = 0; r < rows.Count; r++)
        {
            xml.Append(CultureInfo.InvariantCulture, $"""<row r="{r + 1}">""");
            for (int c = 0; c < rows[r].Count; c++)
            {
                Cell cell = rows[r][c];
                string style = cell.Style == 0 ? string.Empty : $" s=\"{cell.Style.ToString(CultureInfo.InvariantCulture)}\"";
                xml.Append(CultureInfo.InvariantCulture, $"""<c r="{ColumnName(c)}{r + 1}"{style} t="inlineStr"><is><t xml:space="preserve">{Escape(cell.Value)}</t></is></c>""");
            }

            xml.Append("</row>");
        }

        return xml.Append("</sheetData>").Append(validations).Append("</worksheet>").ToString();
    }

    private static string Workbook(string[] sheets) =>
        $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="{Main}" xmlns:r="{Relationships}"><bookViews><workbookView/></bookViews><sheets>{string.Concat(sheets.Select((name, i) => $"""<sheet name="{Escape(name)}" sheetId="{i + 1}" r:id="rId{i + 1}"/>"""))}</sheets></workbook>""";

    private static string WorkbookRelationships(int sheets) =>
        $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{PackageRelationships}">{string.Concat(Enumerable.Range(1, sheets).Select(i => $"""<Relationship Id="rId{i}" Type="{Relationships}/worksheet" Target="worksheets/sheet{i}.xml"/>"""))}<Relationship Id="rId{sheets + 1}" Type="{Relationships}/styles" Target="styles.xml"/></Relationships>""";

    private static string ContentTypes(int sheets) =>
        $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>{string.Concat(Enumerable.Range(1, sheets).Select(i => $"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>"""))}<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""";

    // Text format (49) on the data columns keeps a code such as 007 or a date typed as 2025-01-31 as the text typed.
    private static string Styles() =>
        $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="{Main}"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFFE699"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="6"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="49" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1" applyNumberFormat="1"/><xf numFmtId="49" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyNumberFormat="1"/><xf numFmtId="49" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment wrapText="1" vertical="top"/></xf><xf numFmtId="49" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" quotePrefix="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""";

    /// <summary>The relationships of a part, by id, with each target resolved against <paramref name="folder"/>; none when the part is absent.</summary>
    private static Dictionary<string, Relationship> ReadRelationships(ZipArchive zip, string path, string folder)
    {
        Dictionary<string, Relationship> relationships = new(StringComparer.Ordinal);
        if (zip.GetEntry(path) is not { } entry)
        {
            return relationships;
        }

        using XmlReader reader = Open(entry);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship"
                && reader.GetAttribute("Id") is { } id && reader.GetAttribute("Target") is { } target)
            {
                string resolved = target.StartsWith('/') ? target[1..] : Normalize(folder + target);
                relationships[id] = new Relationship(reader.GetAttribute("Type") ?? string.Empty, resolved);
            }
        }

        return relationships;
    }

    /// <summary>A target such as <c>xl/worksheets/../sharedStrings.xml</c> as the entry name it is.</summary>
    private static string Normalize(string path)
    {
        Stack<string> segments = new();
        foreach (string segment in path.Split('/'))
        {
            if (segment == "..")
            {
                segments.TryPop(out _);
            }
            else if (segment is not ("." or ""))
            {
                segments.Push(segment);
            }
        }

        return string.Join('/', segments.Reverse());
    }

    /// <summary>The relationship id of the sheet named Data, or of the first sheet.</summary>
    private static string? SheetId(ZipArchive zip, string workbookPath)
    {
        if (zip.GetEntry(workbookPath) is not { } entry)
        {
            return null;
        }

        string? first = null;
        using XmlReader reader = Open(entry);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "sheet")
            {
                string? id = RelationshipId(reader);
                if (string.Equals(reader.GetAttribute("name"), DataSheet, StringComparison.OrdinalIgnoreCase))
                {
                    return id;
                }

                first ??= id;
            }
        }

        return first;
    }

    private static string? RelationshipId(XmlReader reader)
    {
        for (int i = 0; i < reader.AttributeCount; i++)
        {
            reader.MoveToAttribute(i);
            if (reader.LocalName == "id" && reader.NamespaceURI.Contains("relationships", StringComparison.Ordinal))
            {
                string value = reader.Value;
                reader.MoveToElement();
                return value;
            }
        }

        reader.MoveToElement();
        return null;
    }

    private static List<string> SharedStrings(ZipArchive zip, string path)
    {
        List<string> strings = [];
        if (zip.GetEntry(path) is not { } entry)
        {
            return strings;
        }

        using XmlReader reader = Open(entry);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si")
            {
                strings.Add(reader.IsEmptyElement ? string.Empty : TextOf(reader));
            }
        }

        return strings;
    }

    /// <summary>The text of an <c>si</c> or <c>is</c> element: its runs' <c>t</c>, without phonetic runs. Leaves the reader on its end tag.</summary>
    private static string TextOf(XmlReader reader)
    {
        StringBuilder text = new();
        int depth = reader.Depth;
        int phonetic = -1;
        bool more = reader.Read();
        while (more && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            if (phonetic < 0 && reader.NodeType == XmlNodeType.Element && reader.LocalName == "t" && !reader.IsEmptyElement)
            {
                // Reading the content moves past the end tag, onto the next node, which the loop must not skip.
                text.Append(reader.ReadElementContentAsString());
                continue;
            }

            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "rPh" && !reader.IsEmptyElement)
            {
                phonetic = reader.Depth;
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == phonetic)
            {
                phonetic = -1;
            }

            more = reader.Read();
        }

        return text.ToString();
    }

    private static ImportSheet? Rows(ZipArchive zip, string path, IReadOnlyList<string> sharedStrings, int rowLimit)
    {
        if (zip.GetEntry(path) is not { } entry)
        {
            return null;
        }

        List<ImportSheetRow> rows = [];
        using XmlReader reader = Open(entry);
        int rowNumber = 0;
        while (rows.Count < rowLimit && reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "row")
            {
                rowNumber = int.TryParse(reader.GetAttribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out int r) ? r : rowNumber + 1;
                Dictionary<int, string> cells = reader.IsEmptyElement ? [] : Cells(reader, sharedStrings);
                if (cells.Count > 0)
                {
                    rows.Add(new ImportSheetRow(rowNumber, cells));
                }
            }
        }

        return new ImportSheet(rows);
    }

    /// <summary>The non-empty cells of the current row, by column index. Leaves the reader on the row's end tag.</summary>
    private static Dictionary<int, string> Cells(XmlReader reader, IReadOnlyList<string> sharedStrings)
    {
        Dictionary<int, string> cells = [];
        int depth = reader.Depth;
        int column = -1;
        while (reader.Read() && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "c")
            {
                continue;
            }

            column = reader.GetAttribute("r") is { } reference ? ColumnIndex(reference) : column + 1;
            string type = reader.GetAttribute("t") ?? "n";
            if (column < 0 || reader.IsEmptyElement)
            {
                continue;
            }

            string? raw = null;
            int cellDepth = reader.Depth;
            bool more = reader.Read();
            while (more && !(reader.NodeType == XmlNodeType.EndElement && reader.Depth == cellDepth))
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "v" && !reader.IsEmptyElement)
                {
                    raw = reader.ReadElementContentAsString();
                    continue;
                }

                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "is" && !reader.IsEmptyElement)
                {
                    raw = TextOf(reader);
                }

                more = reader.Read();
            }

            string? value = (type, raw) switch
            {
                (_, null) => null,
                ("s", _) => sharedStrings[int.Parse(raw, NumberStyles.None, CultureInfo.InvariantCulture)],
                ("b", _) => raw == "1" ? "TRUE" : "FALSE",
                ("d", { Length: >= 10 }) => raw[..10],
                _ => raw,
            };
            if (!string.IsNullOrWhiteSpace(value))
            {
                cells[column] = value;
            }
        }

        return cells;
    }

    private static XmlReader Open(ZipArchiveEntry entry) =>
        entry.Length > MaxPartBytes
            ? throw new InvalidDataException($"Part {entry.FullName} is larger than an import reads.")
            : XmlReader.Create(new CappedStream(entry.Open(), MaxPartBytes), ReaderSettings);

    private sealed record Relationship(string Type, string Target);

    private sealed record Cell(string Value, int Style);

    /// <summary>A part's decompressed bytes, refused past the cap whatever the archive declares about the part's size.</summary>
    private sealed class CappedStream(Stream inner, long cap) : Stream
    {
        private long _read;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _read;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = inner.Read(buffer, offset, count);
            _read += read;
            return _read > cap ? throw new InvalidDataException("A workbook part is larger than an import reads.") : read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
