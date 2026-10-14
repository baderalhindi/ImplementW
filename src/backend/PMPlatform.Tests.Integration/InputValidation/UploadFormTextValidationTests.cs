using System.Net;
using System.Net.Http.Headers;
using PMPlatform.Tests.Integration.DocumentManagement;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.InputValidation;

/// <summary>
/// TASK-077: an upload is a form MVC does not bind, so the filter never sees it; its fields and its file's name obey the same text rules
/// where the form is read (<c>DocumentUploadForm</c>).
/// </summary>
[Collection(DocumentSuite.Name)]
public sealed class UploadFormTextValidationTests(DocumentTestHost host)
{
    [Fact]
    public async Task AnUploadFormFieldOrFileNameHoldingAControlCharacterIsRefused()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;

        using (HttpResponseMessage field = await client.SendMultipartAsync(DocumentDriver.Documents, token, Form("nul \u0000 title", "evidence.txt"), idempotencyKey: null))
        {
            Assert.Equal(HttpStatusCode.BadRequest, field.StatusCode);
            Assert.Equal(["title.text INVALID_CHARACTER"], await field.ReadFieldErrorsAsync());
        }

        using HttpResponseMessage fileName = await client.SendMultipartAsync(DocumentDriver.Documents, token, Form("Evidence", "evi\u0007dence.txt"), idempotencyKey: null);
        Assert.Equal(HttpStatusCode.BadRequest, fileName.StatusCode);
        Assert.Equal(["file INVALID_CHARACTER"], await fileName.ReadFieldErrorsAsync());
    }

    private static MultipartFormDataContent Form(string title, string fileName)
    {
        ByteArrayContent file = new(DocumentDriver.Text("ok"));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        return new MultipartFormDataContent
        {
            { new StringContent(title), "title.text" },
            { new StringContent("en"), "title.language" },
            { new StringContent(DocumentTestHost.DocumentTypeId.ToString()), "documentTypeItemId" },
            { new StringContent(DocumentTestHost.Internal.ToString()), "dataClassificationItemId" },
            { file, "file", fileName },
        };
    }
}
