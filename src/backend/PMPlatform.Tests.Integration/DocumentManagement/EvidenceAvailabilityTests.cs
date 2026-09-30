using System.Net;
using System.Text.Json.Nodes;
using Npgsql;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.DocumentManagement.Contracts.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.DocumentManagement;

/// <summary>
/// TASK-037 acceptance criterion 1 and the workbook's first validation check: a document SCAN_PENDING or QUARANTINED
/// cannot satisfy an evidence requirement or be downloaded through the normal evidence path. And a later version never
/// alters evidence pinned to an earlier one.
/// </summary>
[Collection(DocumentSuite.Name)]
public sealed class EvidenceAvailabilityTests(DocumentTestHost host)
{
    /// <summary>The workbook's check: an EICAR upload lands QUARANTINED and cannot be retrieved via the evidence endpoint.</summary>
    [Fact]
    public async Task AnEicarUploadIsQuarantinedAndIsNeverServedNorEvidence()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;
        (Guid documentId, Guid versionId) = await client.UploadOrFailAsync(token, TestMalwareScanner.Eicar);
        BusinessTarget target = DocumentDriver.NewTarget();
        BusinessLinkDetail link = (await host.Api.LinkAsync(2, documentId, target)).Succeeded();
        string content = $"{DocumentDriver.Documents}/{documentId}/versions/{versionId}/content";

        // SCAN_PENDING: not served, not evidence.
        Assert.Equal("SCAN_PENDING", await host.ScanStateAsync(versionId));
        await AssertNotAvailableAsync(client.GetAsync(content, token));
        await AssertNotEvidenceAsync(await host.Api.DesignateAsync(2, link.Id, versionId));

        // Scanned: QUARANTINED, with the signature the scanner matched.
        Assert.True(await host.Api.ScanAsync() >= 1);
        Assert.Equal("QUARANTINED", await host.ScanStateAsync(versionId));
        using (HttpResponseMessage version = await client.GetAsync($"{DocumentDriver.Documents}/{documentId}/versions/{versionId}", token))
        {
            Assert.Equal("QUARANTINED", (await version.ReadObjectAsync())["scanState"]!.GetValue<string>());
        }

        Assert.Equal(TestMalwareScanner.EicarSignature,
            (await host.Database.QueryAsync($"SELECT scan_reference FROM document_management.document_version WHERE id = '{versionId}'")).Single());

        // Still not served, and the attempt is audited; still not evidence, in the application and in the database.
        await AssertNotAvailableAsync(client.GetAsync(content, token));
        Assert.Equal(["QUARANTINED"], await host.Database.QueryAsync($"""
            SELECT a.new_value FROM audit_activity.audit_event e JOIN audit_activity.audit_event_attribute a ON a.audit_event_id = e.id
            WHERE e.event_type = '{DocumentAuditEvents.QuarantinedContentWithheld}' AND e.subject_id = '{versionId}' AND a.attribute_name = 'scan_state'
            LIMIT 1
            """));
        await AssertNotEvidenceAsync(await host.Api.DesignateAsync(2, link.Id, versionId));
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync($"""
            INSERT INTO document_management.evidence_reference (id, business_link_id, document_version_id, evidence_type_item_id, designated_by_user_id, designated_at, status, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{link.Id}', '{versionId}', '{DocumentTestHost.EvidenceTypeId}', '{IdentityDatabase.UserId(2)}', now(), 'VALID', now(), '{IdentityDatabase.UserId(2)}', now(), '{IdentityDatabase.UserId(2)}')
            """));
        Assert.Equal("ck_evidence_reference_clean_version", refused.ConstraintName);
        Assert.Empty(await host.Api.SatisfiedAsync(target));

        // The bytes are kept, never deleted: quarantine withholds them.
        Assert.Single(Directory.GetFiles(Path.Combine(host.StoreDirectory, "documents", documentId.ToString("N"))));
    }

    /// <summary>
    /// A CLEAN version is evidence and satisfies its type. A new version, pending, cannot be pinned; once CLEAN it can, and
    /// the earlier pin — its row and the bytes the evidence path serves — is exactly as it was.
    /// </summary>
    [Fact]
    public async Task ANewVersionNeverAltersEvidencePinnedToAnEarlierOne()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;
        byte[] first = DocumentDriver.Text($"Signed completion certificate {Guid.NewGuid()}");
        byte[] second = DocumentDriver.Text($"Amended certificate {Guid.NewGuid()}");
        (Guid documentId, Guid v1) = await client.UploadOrFailAsync(token, first);
        await host.Api.ScanAsync();
        BusinessTarget target = DocumentDriver.NewTarget();
        BusinessLinkDetail link = (await host.Api.LinkAsync(2, documentId, target)).Succeeded();
        EvidenceReferenceDetail pinned = (await host.Api.DesignateAsync(2, link.Id, v1)).Succeeded();
        Assert.True(pinned.Satisfies);
        Assert.Equal([DocumentTestHost.EvidenceTypeId], await host.Api.SatisfiedAsync(target));
        IReadOnlyList<string> pinnedRows = await host.Database.QueryAsync(
            $"SELECT to_jsonb(r)::text FROM document_management.document_version r WHERE r.id = '{v1}' UNION ALL SELECT to_jsonb(e)::text FROM document_management.evidence_reference e WHERE e.id = '{pinned.Id}'");

        using HttpResponseMessage added = await client.AddVersionAsync(token, documentId, second);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Guid v2 = AdministrationApi.IdOf(await added.ReadObjectAsync());
        await AssertNotEvidenceAsync(await host.Api.DesignateAsync(2, link.Id, v2));
        await host.Api.ScanAsync();
        EvidenceReferenceDetail repinned = (await host.Api.DesignateAsync(2, link.Id, v2)).Succeeded();

        Assert.NotEqual(pinned.Id, repinned.Id);
        Assert.Equal(pinnedRows, await host.Database.QueryAsync(
            $"SELECT to_jsonb(r)::text FROM document_management.document_version r WHERE r.id = '{v1}' UNION ALL SELECT to_jsonb(e)::text FROM document_management.evidence_reference e WHERE e.id = '{pinned.Id}'"));
        using HttpResponseMessage evidence = await client.GetAsync($"{DocumentDriver.EvidenceReferences}/{pinned.Id}/content", token);
        Assert.Equal(HttpStatusCode.OK, evidence.StatusCode);
        Assert.Equal(first, await evidence.ReadBytesAsync());
        using HttpResponseMessage evidenceDetail = await client.GetAsync($"{DocumentDriver.EvidenceReferences}/{pinned.Id}", token);
        JsonObject body = await evidenceDetail.ReadObjectAsync();
        Assert.Equal((1, v1), (body["versionNo"]!.GetValue<int>(), Guid.Parse(body["documentVersionId"]!.GetValue<string>())));
    }

    /// <summary>The evidence path for a pinned version that later could not be served: none can exist, because only CLEAN is pinned and CLEAN is final.</summary>
    [Fact]
    public async Task APinnedVersionsVerdictCanNeverBeChangedInTheDatabase()
    {
        using HttpClient client = host.Api.CreateClient();
        string token = (await client.SignInOrFailAsync(2)).AccessToken;
        (_, Guid versionId) = await client.UploadOrFailAsync(token, DocumentDriver.Text($"clean {Guid.NewGuid()}"));
        await host.Api.ScanAsync();

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(
            $"UPDATE document_management.document_version SET scan_state = 'QUARANTINED' WHERE id = '{versionId}'"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, refused.SqlState);
    }

    private static async Task AssertNotAvailableAsync(Task<HttpResponseMessage> call)
    {
        using HttpResponseMessage response = await call;
        Assert.Equal((HttpStatusCode.Conflict, DocumentErrorCodes.NotAvailable), (response.StatusCode, await response.CodeOfAsync()));
    }

    private static Task AssertNotEvidenceAsync(AdministrationResult<EvidenceReferenceDetail> result)
    {
        Assert.False(result.Succeeded);
        Assert.Equal((AdministrationErrorKind.Conflict, DocumentErrorCodes.NotAvailable), (result.Error.Kind, result.Error.Code));
        return Task.CompletedTask;
    }
}
