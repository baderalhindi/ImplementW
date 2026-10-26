using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.DocumentManagement;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.DocumentManagement;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.DocumentManagement;

/// <summary>WF-12 as the SPA and an owning module reach it; each in-process call is its own scope, as a request is.</summary>
internal static class DocumentDriver
{
    public const string Documents = "/api/v1/documents";
    public const string EvidenceReferences = "/api/v1/evidence-references";

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    /// <summary>A milestone achievement, as Milestone (TASK-050) would name it: a target DocumentManagement never reads.</summary>
    public static BusinessTarget NewTarget() => new("Milestone", "MilestoneAchievement", Guid.NewGuid());

    public static byte[] Text(string content) => Encoding.UTF8.GetBytes(content);

    public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>
    /// MOD-050 as the SPA sends it: multipart, the file in part <c>file</c>, the metadata as named fields. A fresh
    /// Idempotency-Key unless <paramref name="idempotencyKey"/> says otherwise ("" sends none).
    /// </summary>
    public static Task<HttpResponseMessage> UploadAsync(
        this HttpClient client, string token, byte[] bytes, Guid? projectId = null, Guid? classification = null,
        string fileName = "evidence.txt", string contentType = "text/plain", Guid? documentType = null, string? idempotencyKey = null)
    {
        MultipartFormDataContent form = new()
        {
            { new StringContent("Achievement evidence"), "title.text" },
            { new StringContent("en"), "title.language" },
            { new StringContent((documentType ?? DocumentTestHost.DocumentTypeId).ToString()), "documentTypeItemId" },
            { new StringContent((classification ?? DocumentTestHost.Internal).ToString()), "dataClassificationItemId" },
        };
        if (projectId is { } project)
        {
            form.Add(new StringContent(project.ToString()), "projectId");
        }

        form.Add(File(bytes, contentType), DocumentUploadFormPart, fileName);
        return client.SendMultipartAsync(Documents, token, form, idempotencyKey);
    }

    /// <summary>MOD-052: a new file for the document.</summary>
    public static Task<HttpResponseMessage> AddVersionAsync(this HttpClient client, string token, Guid documentId, byte[] bytes, string contentType = "text/plain")
    {
        MultipartFormDataContent form = new() { { File(bytes, contentType), DocumentUploadFormPart, "evidence-v2.txt" } };
        return client.SendMultipartAsync($"{Documents}/{documentId}/versions", token, form, idempotencyKey: null);
    }

    public static async Task<HttpResponseMessage> SendMultipartAsync(this HttpClient client, string path, string token, HttpContent content, string? idempotencyKey)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        idempotencyKey ??= Guid.NewGuid().ToString();
        if (idempotencyKey.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request);
    }

    /// <summary>Uploads and returns (document, version 1), asserting 201.</summary>
    public static async Task<(Guid DocumentId, Guid VersionId)> UploadOrFailAsync(
        this HttpClient client, string token, byte[] bytes, Guid? projectId = null, Guid? classification = null)
    {
        using HttpResponseMessage created = await client.UploadAsync(token, bytes, projectId, classification);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonObject body = await created.ReadObjectAsync();
        return (AdministrationApi.IdOf(body), Guid.Parse(body["latestVersion"]!["id"]!.GetValue<string>()));
    }

    /// <summary>One pass of the scan worker, now.</summary>
    public static async Task<int> ScanAsync(this IdentityApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDocumentScanning>().RunAsync(100, CancellationToken.None);
    }

    public static async Task<T> LinksAsync<T>(this IdentityApiFactory api, Func<IDocumentLinks, Task<T>> call)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        return await call(scope.ServiceProvider.GetRequiredService<IDocumentLinks>());
    }

    public static Task<AdministrationResult<BusinessLinkDetail>> LinkAsync(this IdentityApiFactory api, int person, Guid documentId, BusinessTarget target) =>
        api.LinksAsync(links => links.LinkAsync(Person(person), documentId, target, BusinessLinkRole.Attachment, CancellationToken.None));

    public static Task<AdministrationResult<EvidenceReferenceDetail>> DesignateAsync(this IdentityApiFactory api, int person, Guid linkId, Guid versionId) =>
        api.LinksAsync(links => links.DesignateEvidenceAsync(Person(person), new EvidenceDesignation(linkId, versionId, DocumentTestHost.EvidenceTypeId), CancellationToken.None));

    public static Task<IReadOnlySet<Guid>> SatisfiedAsync(this IdentityApiFactory api, BusinessTarget target) =>
        api.LinksAsync(links => links.GetSatisfiedEvidenceTypesAsync(target, CancellationToken.None));

    public static T Succeeded<T>(this AdministrationResult<T> result)
        where T : class
    {
        Assert.True(result.Succeeded, $"Refused: {result.Error}");
        return result.Value;
    }

    public static async Task<string> ScanStateAsync(this DocumentTestHost host, Guid versionId) =>
        (await host.Database.QueryAsync($"SELECT scan_state FROM document_management.document_version WHERE id = '{versionId}'")).Single();

    /// <summary>Every column of every row of the four tables that names the document, as JSON: its whole stored history.</summary>
    public static Task<IReadOnlyList<string>> HistoryAsync(this DocumentTestHost host, Guid documentId) =>
        host.Database.QueryAsync($"""
            SELECT to_jsonb(d)::text FROM document_management.document d WHERE d.id = '{documentId}'
            UNION ALL SELECT to_jsonb(v)::text FROM document_management.document_version v WHERE v.document_id = '{documentId}'
            UNION ALL SELECT to_jsonb(l)::text FROM document_management.business_link l WHERE l.document_id = '{documentId}'
            UNION ALL SELECT to_jsonb(e)::text FROM document_management.evidence_reference e
                      JOIN document_management.business_link l ON l.id = e.business_link_id WHERE l.document_id = '{documentId}'
            ORDER BY 1
            """);

    public static async Task<byte[]> ReadBytesAsync(this HttpResponseMessage response) => await response.Content.ReadAsByteArrayAsync();

    private const string DocumentUploadFormPart = "file";

    private static ByteArrayContent File(byte[] bytes, string contentType)
    {
        ByteArrayContent file = new(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return file;
    }
}
