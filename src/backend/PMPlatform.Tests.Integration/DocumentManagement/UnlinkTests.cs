using System.Net;
using System.Text.Json.Nodes;
using Npgsql;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.DocumentManagement;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.DocumentManagement;

/// <summary>
/// TASK-037 acceptance criterion 2 and the workbook's second validation check: unlinking a document from a business
/// context does not delete the underlying file or version history, and the version stays retrievable by an authorized query.
/// </summary>
[Collection(DocumentSuite.Name)]
public sealed class UnlinkTests(DocumentTestHost host)
{
    [Fact]
    public async Task UnlinkingACleanDocumentDeletesNothingAndItStaysRetrievable()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;
        byte[] bytes = DocumentDriver.Text($"Site handover minutes {Guid.NewGuid()}");
        (Guid documentId, Guid versionId) = await client.UploadOrFailAsync(token, bytes);
        await host.Api.ScanAsync();
        BusinessTarget target = DocumentDriver.NewTarget();
        BusinessLinkDetail link = (await host.Api.LinkAsync(2, documentId, target)).Succeeded();
        EvidenceReferenceDetail evidence = (await host.Api.DesignateAsync(2, link.Id, versionId)).Succeeded();
        Assert.Equal([DocumentTestHost.EvidenceTypeId], await host.Api.SatisfiedAsync(target));
        string[] files = Directory.GetFiles(Path.Combine(host.StoreDirectory, "documents", documentId.ToString("N")));
        int rowsBefore = (await host.HistoryAsync(documentId)).Count;

        BusinessLinkDetail unlinked = (await host.Api.LinksAsync(links => links.UnlinkAsync(DocumentDriver.Person(2), link.Id, CancellationToken.None))).Succeeded();

        // The link ended and its evidence was withdrawn: the target no longer holds the evidence, and has no active link.
        Assert.NotNull(unlinked.UnlinkedAt);
        Assert.Equal(EvidenceReferenceStatus.Withdrawn, Assert.Single(unlinked.Evidence).Status);
        Assert.Empty(await host.Api.SatisfiedAsync(target));
        Assert.Empty(await host.Api.LinksAsync(links => links.FindLinksAsync(target, CancellationToken.None)));

        // Nothing was deleted: every row is still there, and so is the file.
        Assert.Equal(rowsBefore, (await host.HistoryAsync(documentId)).Count);
        Assert.Equal(files, Directory.GetFiles(Path.Combine(host.StoreDirectory, "documents", documentId.ToString("N"))));

        // The authorized query: the version, its bytes by both paths, and the ended link with its withdrawn evidence.
        using (HttpResponseMessage versions = await client.GetAsync($"{DocumentDriver.Documents}/{documentId}/versions", token))
        {
            Assert.Equal(versionId, AdministrationApi.IdOf(Assert.Single(await versions.ReadArrayAsync())!.AsObject()));
        }

        foreach (string path in new[] { $"{DocumentDriver.Documents}/{documentId}/versions/{versionId}/content", $"{DocumentDriver.EvidenceReferences}/{evidence.Id}/content" })
        {
            using HttpResponseMessage content = await client.GetAsync(path, token);
            Assert.Equal(HttpStatusCode.OK, content.StatusCode);
            Assert.Equal(DocumentDriver.Sha256(bytes), DocumentDriver.Sha256(await content.ReadBytesAsync()));
        }

        using (HttpResponseMessage links = await client.GetAsync($"{DocumentDriver.Documents}/{documentId}/links", token))
        {
            JsonObject history = Assert.Single(await links.ReadArrayAsync())!.AsObject();
            Assert.NotNull(history["unlinkedAt"]?.GetValue<DateTimeOffset>());
            Assert.Equal("WITHDRAWN", history["evidence"]![0]!["status"]!.GetValue<string>());
            Assert.False(history["evidence"]![0]!["satisfies"]!.GetValue<bool>());
        }
    }

    /// <summary>An ended link stays ended: it takes no evidence, cannot be unlinked twice, and the same link cannot be made again (F-7).</summary>
    [Fact]
    public async Task AnEndedLinkStaysEnded()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;
        (Guid documentId, Guid versionId) = await client.UploadOrFailAsync(token, DocumentDriver.Text($"minutes {Guid.NewGuid()}"));
        await host.Api.ScanAsync();
        BusinessTarget target = DocumentDriver.NewTarget();
        BusinessLinkDetail link = (await host.Api.LinkAsync(2, documentId, target)).Succeeded();
        Assert.Equal(link.Id, (await host.Api.LinkAsync(2, documentId, target)).Succeeded().Id);
        (await host.Api.LinksAsync(links => links.UnlinkAsync(DocumentDriver.Person(2), link.Id, CancellationToken.None))).Succeeded();

        Assert.Equal(AdministrationErrorKind.TerminalState,
            (await host.Api.LinksAsync(links => links.UnlinkAsync(DocumentDriver.Person(2), link.Id, CancellationToken.None))).Error!.Kind);
        Assert.Equal(DocumentErrorCodes.LinkEnded, (await host.Api.DesignateAsync(2, link.Id, versionId)).Error!.Code);
        Assert.Equal(DocumentErrorCodes.LinkEnded, (await host.Api.LinkAsync(2, documentId, target)).Error!.Code);
    }

    /// <summary>RETAIN, in the database: no row of the four tables is deleted, whoever tries, and an ended link is not reopened.</summary>
    [Fact]
    public async Task TheDatabaseRefusesToDeleteOrReopenDocumentHistory()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;
        (Guid documentId, Guid versionId) = await client.UploadOrFailAsync(token, DocumentDriver.Text($"record {Guid.NewGuid()}"));
        await host.Api.ScanAsync();
        BusinessLinkDetail link = (await host.Api.LinkAsync(2, documentId, DocumentDriver.NewTarget())).Succeeded();
        EvidenceReferenceDetail evidence = (await host.Api.DesignateAsync(2, link.Id, versionId)).Succeeded();
        (await host.Api.LinksAsync(links => links.UnlinkAsync(DocumentDriver.Person(2), link.Id, CancellationToken.None))).Succeeded();

        string[] refused =
        [
            $"DELETE FROM document_management.evidence_reference WHERE id = '{evidence.Id}'",
            $"DELETE FROM document_management.business_link WHERE id = '{link.Id}'",
            $"DELETE FROM document_management.document_version WHERE id = '{versionId}'",
            $"DELETE FROM document_management.document WHERE id = '{documentId}'",
            $"UPDATE document_management.business_link SET unlinked_at = NULL, unlinked_by_user_id = NULL WHERE id = '{link.Id}'",
            $"UPDATE document_management.evidence_reference SET status = 'VALID' WHERE id = '{evidence.Id}'",
            $"UPDATE document_management.document_version SET checksum_sha256 = repeat('0', 64) WHERE id = '{versionId}'",
            $"UPDATE document_management.document_version SET storage_object_key = 'documents/elsewhere' WHERE id = '{versionId}'",
            $"UPDATE document_management.document SET project_id = '{DocumentTestHost.EntityProjectId}' WHERE id = '{documentId}'",
        ];

        foreach (string statement in refused)
        {
            PostgresException exception = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(statement));
            Assert.Equal(PostgresErrorCodes.RestrictViolation, exception.SqlState);
        }

        PostgresException bornClean = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync($"""
            INSERT INTO document_management.document_version (id, document_id, version_no, storage_object_key, file_name, content_type, size_bytes, checksum_sha256,
                                                              uploaded_by_user_id, uploaded_at, scan_state, scan_completed_at, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{documentId}', 9, 'documents/forged', 'forged.txt', 'text/plain', 1, repeat('a', 64),
                    '{IdentityDatabase.UserId(2)}', now(), 'CLEAN', now(), now(), '{IdentityDatabase.UserId(2)}', now(), '{IdentityDatabase.UserId(2)}')
            """));
        Assert.Equal("ck_document_version_born_pending", bornClean.ConstraintName);
    }
}
