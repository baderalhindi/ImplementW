using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.DocumentManagement;

/// <summary>The WF-12 endpoints as the SPA calls them (R-8, R-21, R-23, R-35, R-47): validation, limits, headers and lifecycle.</summary>
[Collection(DocumentSuite.Name)]
public sealed class DocumentEndpointTests(DocumentTestHost host)
{
    [Fact]
    public async Task AnUploadIsValidatedLimitedAndTypedBeforeAnythingIsStored()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;
        byte[] bytes = DocumentDriver.Text("ok");

        await AssertAnswerAsync(HttpStatusCode.BadRequest, "IDEMPOTENCY_KEY_REQUIRED", client.UploadAsync(token, bytes, idempotencyKey: ""));
        await AssertAnswerAsync(HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE",
            client.SendMultipartAsync(DocumentDriver.Documents, token, JsonContent.Create(new { title = "not a form" }), idempotencyKey: null));
        await AssertAnswerAsync(HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE", client.UploadAsync(token, bytes, contentType: "application/x-msdownload"));
        await AssertAnswerAsync(HttpStatusCode.RequestEntityTooLarge, "PAYLOAD_TOO_LARGE", client.UploadAsync(token, new byte[DocumentTestHost.MaxFileSizeBytes + 1]));

        using (HttpResponseMessage empty = await client.SendMultipartAsync(DocumentDriver.Documents, token, new MultipartFormDataContent { { new StringContent("x"), "unrelated" } }, null))
        {
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
            Assert.Equal(
                ["title.text REQUIRED", "title.language REQUIRED", "documentTypeItemId REQUIRED", "dataClassificationItemId REQUIRED", "file REQUIRED"],
                await empty.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage draftType = await client.UploadAsync(token, bytes, documentType: DocumentTestHost.DraftDocumentTypeId))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "DOCUMENT_REFERENCE_INVALID"), (draftType.StatusCode, await draftType.CodeOfAsync()));
            Assert.Equal(["documentTypeItemId NOT_FOUND"], await draftType.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage noProject = await client.UploadAsync(token, bytes, Guid.NewGuid()))
        {
            Assert.Equal(["projectId NOT_FOUND"], await noProject.ReadFieldErrorsAsync());
        }

        // Accepted: the platform's name for the file is its last segment; the checksum is of the bytes stored.
        byte[] accepted = DocumentDriver.Text($"accepted {Guid.NewGuid()}");
        using HttpResponseMessage created = await client.UploadAsync(token, accepted, fileName: @"..\..\C$\minutes.txt");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonObject body = await created.ReadObjectAsync();
        Assert.Equal($"{DocumentDriver.Documents}/{AdministrationApi.IdOf(body)}", created.Headers.Location!.OriginalString);
        Assert.NotNull(created.Headers.ETag);
        JsonNode version = body["latestVersion"]!;
        Assert.Equal(
            ("minutes.txt", DocumentDriver.Sha256(accepted), accepted.LongLength, "SCAN_PENDING", 1),
            (version["fileName"]!.GetValue<string>(), version["checksumSha256"]!.GetValue<string>(), version["sizeBytes"]!.GetValue<long>(),
             version["scanState"]!.GetValue<string>(), version["versionNo"]!.GetValue<int>()));
        Assert.Null(version["storageObjectKey"]);
    }

    [Fact]
    public async Task ContentIsServedAsAnAttachmentThatIsNeverSniffedNorCached()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;
        byte[] bytes = DocumentDriver.Text($"<html><script>alert(1)</script></html> {Guid.NewGuid()}");
        (Guid documentId, Guid versionId) = await client.UploadOrFailAsync(token, bytes);
        await host.Api.ScanAsync();

        using HttpResponseMessage content = await client.GetAsync($"{DocumentDriver.Documents}/{documentId}/versions/{versionId}/content", token);

        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("application/octet-stream", content.Content.Headers.ContentType!.MediaType);
        Assert.Equal(("attachment", "evidence.txt"), (content.Content.Headers.ContentDisposition!.DispositionType, content.Content.Headers.ContentDisposition.FileName));
        Assert.Equal("nosniff", content.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(content.Headers.CacheControl!.NoStore);
        Assert.Equal(bytes, await content.ReadBytesAsync());
    }

    [Fact]
    public async Task MetadataIsEditedUnderIfMatchAndAnArchivedDocumentTakesNoChange()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;
        (Guid documentId, _) = await client.UploadOrFailAsync(token, DocumentDriver.Text($"metadata {Guid.NewGuid()}"));
        string path = $"{DocumentDriver.Documents}/{documentId}";
        object changes = new
        {
            title = new { text = "Renamed report", language = "en" },
            documentTypeItemId = DocumentTestHost.DocumentTypeId,
            dataClassificationItemId = DocumentTestHost.Internal,
        };
        using HttpResponseMessage read = await client.GetAsync(path, token);
        string etag = AdministrationApi.ETagOf(read);

        await AssertAnswerAsync(HttpStatusCode.PreconditionRequired, "PRECONDITION_REQUIRED", client.SendAsync(HttpMethod.Put, path, token, changes));
        await AssertAnswerAsync(HttpStatusCode.PreconditionFailed, "PRECONDITION_FAILED", client.PutAsync(path, token, changes, "\"1\""));
        await AssertAnswerAsync(HttpStatusCode.Forbidden, "PERMISSION_DENIED",
            client.PutAsync(path, token, new { title = new { text = "x", language = "en" }, documentTypeItemId = DocumentTestHost.DocumentTypeId, dataClassificationItemId = DocumentTestHost.Confidential }, etag));

        using (HttpResponseMessage updated = await client.PutAsync(path, token, changes, etag))
        {
            Assert.Equal("Renamed report", (await updated.ReadObjectAsync())["title"]!["text"]!.GetValue<string>());
            Assert.NotEqual(etag, AdministrationApi.ETagOf(updated));
        }

        using (HttpResponseMessage archived = await client.PostAsync($"{path}/archive", token))
        {
            Assert.Equal("ARCHIVED", (await archived.ReadObjectAsync())["status"]!.GetValue<string>());
        }

        await AssertAnswerAsync(HttpStatusCode.Conflict, "TERMINAL_STATE", client.PostAsync($"{path}/archive", token));
        await AssertAnswerAsync(HttpStatusCode.Conflict, "TERMINAL_STATE", client.AddVersionAsync(token, documentId, DocumentDriver.Text("late")));
        using HttpResponseMessage versions = await client.GetAsync($"{path}/versions", token);
        Assert.Equal(HttpStatusCode.OK, versions.StatusCode);
    }

    [Fact]
    public async Task AnUnknownDocumentOrEvidenceIsNotFound()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;
        Guid unknown = Guid.NewGuid();

        foreach (string path in new[]
                 {
                     $"{DocumentDriver.Documents}/{unknown}", $"{DocumentDriver.Documents}/{unknown}/versions", $"{DocumentDriver.Documents}/{unknown}/links",
                     $"{DocumentDriver.Documents}/{unknown}/versions/{unknown}/content", $"{DocumentDriver.EvidenceReferences}/{unknown}",
                     $"{DocumentDriver.EvidenceReferences}/{unknown}/content",
                 })
        {
            await AssertAnswerAsync(HttpStatusCode.NotFound, "NOT_FOUND", client.GetAsync(path, token));
        }
    }

    private static async Task AssertAnswerAsync(HttpStatusCode status, string code, Task<HttpResponseMessage> call)
    {
        using HttpResponseMessage response = await call;
        Assert.Equal((status, code), (response.StatusCode, await response.CodeOfAsync()));
    }
}
