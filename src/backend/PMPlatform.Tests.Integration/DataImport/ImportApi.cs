using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.Json.Nodes;

namespace PMPlatform.Tests.Integration.DataImport;

/// <summary>The migration import endpoints (TASK-102) as the SPA calls them, and the workbooks a user fills in.</summary>
internal static class ImportApi
{
    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>The workbook in part <c>file</c>, with the Idempotency-Key every sensitive write carries.</summary>
    public static Task<HttpResponseMessage> UploadAsync(this HttpClient client, string collection, string accessToken, byte[] workbook, bool commit)
    {
        HttpRequestMessage request = new(HttpMethod.Post, $"{collection}/imports{(commit ? "?commit=true" : string.Empty)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
        ByteArrayContent file = new(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        request.Content = new MultipartFormDataContent { { file, "file", "migration.xlsx" } };
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> TemplateAsync(this HttpClient client, string collection, string accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"{collection}/import-template/content");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    /// <summary>The issues of a report as "row column CODE", in order.</summary>
    public static string[] IssuesOf(JsonObject report) =>
        [.. report["issues"]!.AsArray().Select(i => $"{i!["row"]} {i["column"]} {i["code"]}".Replace("  ", " ", StringComparison.Ordinal))];

    /// <summary>The header row of a workbook's Data sheet, by reading its first row's inline strings.</summary>
    public static string[] HeaderOf(byte[] workbook)
    {
        using ZipArchive zip = new(new MemoryStream(workbook));
        using StreamReader reader = new(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        string sheet = reader.ReadToEnd();
        string firstRow = sheet[sheet.IndexOf("<row r=\"1\">", StringComparison.Ordinal)..sheet.IndexOf("</row>", StringComparison.Ordinal)];
        return [.. firstRow.Split("<t xml:space=\"preserve\">").Skip(1).Select(t => t[..t.IndexOf('<', StringComparison.Ordinal)])];
    }

    /// <summary>A workbook as a spreadsheet application saves one: a Data sheet of inline strings, the header first.</summary>
    public static byte[] Workbook(string[] header, params string?[][] rows)
    {
        StringBuilder sheet = new();
        string?[][] all = [header, .. rows];
        for (int r = 0; r < all.Length; r++)
        {
            sheet.Append(CultureInfo.InvariantCulture, $"<row r=\"{r + 1}\">");
            for (int c = 0; c < all[r].Length; c++)
            {
                if (all[r][c] is { Length: > 0 } value)
                {
                    sheet.Append(CultureInfo.InvariantCulture, $"<c r=\"{(char)('A' + c)}{r + 1}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(value)}</t></is></c>");
                }
            }

            sheet.Append("</row>");
        }

        using MemoryStream buffer = new();
        using (ZipArchive zip = new(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Part(zip, "_rels/.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Part(zip, "xl/workbook.xml", $"""<workbook xmlns="{Main}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Data" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Part(zip, "xl/_rels/workbook.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");
            Part(zip, "xl/worksheets/sheet1.xml", $"""<worksheet xmlns="{Main}"><sheetData>{sheet}</sheetData></worksheet>""");
        }

        return buffer.ToArray();
    }

    private static void Part(ZipArchive zip, string name, string xml)
    {
        using Stream stream = zip.CreateEntry(name).Open();
        stream.Write(Encoding.UTF8.GetBytes(xml));
    }
}
