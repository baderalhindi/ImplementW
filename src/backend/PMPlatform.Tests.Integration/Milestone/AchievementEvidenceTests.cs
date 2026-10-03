using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.Milestone.Contracts;
using PMPlatform.Tests.Integration.DocumentManagement;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Schedule;

namespace PMPlatform.Tests.Integration.Milestone;

/// <summary>
/// WF-05 owns achievement evidence (ICD-04): a revision's evidence is attached and pinned through DocumentManagement (edge 16)
/// while it is a DRAFT, and EVIDENCE_POLICY's mandatory evidence for the milestone's category must be held, CLEAN, before it is
/// submitted (PTBC-006).
/// </summary>
[Collection(MilestoneSuite.Name)]
public sealed class AchievementEvidenceTests(MilestoneTestHost host)
{
    [Fact]
    public async Task MandatoryEvidenceMustBeHeldCleanBeforeSubmission()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, projectId, MilestoneTestHost.HandoverCategoryId);
        Guid achievementId = AdministrationApi.IdOf(await client.ClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));

        using (HttpResponseMessage bare = await client.PostAsync($"{MilestoneDriver.Achievements}/{achievementId}/submit", sessions.ProjectManager))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, MilestoneErrorCodes.EvidenceRequired), await bare.RefusalAsync());
        }

        JsonObject required = await client.EvidenceAsync(sessions.ProjectManager, achievementId);
        Assert.Equal([MilestoneTestHost.EvidenceTypeId.ToString()], Ids(required, "mandatoryEvidenceTypeItemIds"));
        Assert.Empty(Ids(required, "satisfiedEvidenceTypeItemIds"));
        Assert.NotNull(required["evidencePolicyVersionId"]);

        // A version still being scanned cannot be pinned, and nothing is left linked.
        (Guid documentId, Guid versionId) = await client.UploadAsync(sessions.ProjectManager, projectId);
        using (HttpResponseMessage pending = await client.PostAsync($"{MilestoneDriver.Achievements}/{achievementId}/evidence", sessions.ProjectManager, Evidence(documentId, versionId)))
        {
            Assert.Equal((HttpStatusCode.Conflict, DocumentErrorCodes.NotAvailable), await pending.RefusalAsync());
        }

        Assert.Empty((await client.EvidenceAsync(sessions.ProjectManager, achievementId))["links"]!.AsArray());

        await host.Api.ScanAsync();
        using (HttpResponseMessage attached = await client.PostAsync($"{MilestoneDriver.Achievements}/{achievementId}/evidence", sessions.ProjectManager, Evidence(documentId, versionId)))
        {
            Assert.Equal(HttpStatusCode.Created, attached.StatusCode);
            JsonObject reference = await attached.ReadObjectAsync();
            Assert.Equal((versionId.ToString(), true), (reference.Text("documentVersionId"), reference["satisfies"]!.GetValue<bool>()));
        }

        Assert.Equal([MilestoneTestHost.EvidenceTypeId.ToString()], Ids(await client.EvidenceAsync(sessions.ProjectManager, achievementId), "satisfiedEvidenceTypeItemIds"));
        JsonObject submitted = await client.CommandOrFailAsync(sessions.ProjectManager, $"{MilestoneDriver.Achievements}/{achievementId}/submit");
        Assert.Equal("SUBMITTED", submitted.Text("status"));

        // Submitted: what WF-11 reviews is what was evidenced; its evidence changes no more.
        string referenceId = (await client.EvidenceAsync(sessions.ProjectManager, achievementId))["links"]!.AsArray().Single()!["evidence"]!.AsArray().Single()!.Text("id");
        using (HttpResponseMessage more = await client.PostAsync($"{MilestoneDriver.Achievements}/{achievementId}/evidence", sessions.ProjectManager, Evidence(documentId, versionId)))
        {
            Assert.Equal((HttpStatusCode.Conflict, MilestoneErrorCodes.AchievementNotEditable), await more.RefusalAsync());
        }

        using (HttpResponseMessage withdrawn = await client.PostAsync($"{MilestoneDriver.Achievements}/{achievementId}/evidence/{referenceId}/withdraw", sessions.ProjectManager))
        {
            Assert.Equal((HttpStatusCode.Conflict, MilestoneErrorCodes.AchievementNotEditable), await withdrawn.RefusalAsync());
        }

        Assert.Equal(["Milestone.AchievementStarted", "Milestone.EvidenceAttached", "Milestone.AchievementSubmitted"], await host.AuditEventsAsync("Milestone", achievementId));
    }

    /// <summary>
    /// Evidence belongs to its revision: withdrawing a DRAFT's evidence makes it unsatisfied again; deleting a DRAFT ends its links;
    /// another revision's evidence reference is not this one's to withdraw (R-47).
    /// </summary>
    [Fact]
    public async Task EvidenceBelongsToItsRevision()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, projectId, MilestoneTestHost.HandoverCategoryId);
        (Guid documentId, Guid versionId) = await client.UploadAsync(sessions.ProjectManager, projectId);
        await host.Api.ScanAsync();

        Guid first = AdministrationApi.IdOf(await client.ClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));
        JsonObject reference = await client.CreatedOrFailAsync(sessions.ProjectManager, $"{MilestoneDriver.Achievements}/{first}/evidence", Evidence(documentId, versionId));
        JsonObject withdrawn = await client.CommandOrFailAsync(sessions.ProjectManager, $"{MilestoneDriver.Achievements}/{first}/evidence/{reference.Text("id")}/withdraw");
        Assert.Equal(("WITHDRAWN", false), (withdrawn.Text("status"), withdrawn["satisfies"]!.GetValue<bool>()));
        Assert.Empty(Ids(await client.EvidenceAsync(sessions.ProjectManager, first), "satisfiedEvidenceTypeItemIds"));
        using (HttpResponseMessage unsatisfied = await client.PostAsync($"{MilestoneDriver.Achievements}/{first}/submit", sessions.ProjectManager))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, MilestoneErrorCodes.EvidenceRequired), await unsatisfied.RefusalAsync());
        }

        // Withdrawn evidence is not pinned again (document-management.md F-7): a new version of the document is, on the same link.
        using (HttpResponseMessage added = await client.AddVersionAsync(sessions.ProjectManager, documentId, DocumentDriver.Text($"Signed certificate {Guid.NewGuid()}")))
        {
            Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        }

        await host.Api.ScanAsync();
        Guid signed = Guid.Parse(Assert.Single(await host.Database.QueryAsync($"SELECT id::text FROM document_management.document_version WHERE document_id = '{documentId}' AND version_no = 2")));
        await client.CreatedOrFailAsync(sessions.ProjectManager, $"{MilestoneDriver.Achievements}/{first}/evidence", Evidence(documentId, signed));
        Assert.NotEmpty(Ids(await client.EvidenceAsync(sessions.ProjectManager, first), "satisfiedEvidenceTypeItemIds"));

        // Delete the draft: its link ends with it; the document stays.
        using (HttpResponseMessage deleted = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"{MilestoneDriver.Achievements}/{first}") { Headers = { { "Authorization", $"Bearer {sessions.ProjectManager}" } } }))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM document_management.business_link WHERE target_id = '{first}' AND unlinked_at IS NULL"));
        Assert.Equal(["1"], await host.Database.QueryAsync($"SELECT count(*)::text FROM document_management.business_link WHERE target_id = '{first}'"));

        // A new revision holds none of the old one's evidence, and cannot withdraw another revision's.
        Guid second = AdministrationApi.IdOf(await client.ClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));
        Assert.Empty((await client.EvidenceAsync(sessions.ProjectManager, second))["links"]!.AsArray());
        Guid otherMilestone = await client.MilestoneAsync(sessions.ProjectManager, projectId, MilestoneTestHost.HandoverCategoryId);
        Guid other = AdministrationApi.IdOf(await client.ClaimAsync(sessions.ProjectManager, otherMilestone, MilestoneDriver.Today));
        JsonObject othersReference = await client.CreatedOrFailAsync(sessions.ProjectManager, $"{MilestoneDriver.Achievements}/{other}/evidence", Evidence(documentId, versionId));
        using HttpResponseMessage foreign = await client.PostAsync($"{MilestoneDriver.Achievements}/{second}/evidence/{othersReference.Text("id")}/withdraw", sessions.ProjectManager);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    /// <summary>A category EVIDENCE_POLICY names no mandatory evidence for is submitted without any.</summary>
    [Fact]
    public async Task ACategoryWithoutMandatoryEvidenceIsSubmittedWithoutAny()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, await host.ScheduledProjectAsync(client, sessions.ProjectManager), MilestoneTestHost.GeneralCategoryId);
        Guid achievementId = AdministrationApi.IdOf(await client.ClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));

        Assert.Empty(Ids(await client.EvidenceAsync(sessions.ProjectManager, achievementId), "mandatoryEvidenceTypeItemIds"));
        Assert.Equal("SUBMITTED", (await client.CommandOrFailAsync(sessions.ProjectManager, $"{MilestoneDriver.Achievements}/{achievementId}/submit")).Text("status"));
    }

    private static object Evidence(Guid documentId, Guid versionId) =>
        new { documentId, documentVersionId = versionId, evidenceTypeItemId = MilestoneTestHost.EvidenceTypeId };

    private static string[] Ids(JsonObject evidence, string property) => [.. evidence[property]!.AsArray().Select(i => i!.GetValue<string>())];
}

file static class EvidenceDriver
{
    public static async Task<JsonObject> EvidenceAsync(this HttpClient client, string token, Guid achievementId)
    {
        using HttpResponseMessage response = await client.GetAsync($"{MilestoneDriver.Achievements}/{achievementId}/evidence", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadObjectAsync();
    }

    /// <summary>Uploads a certificate to the project as the caller, classified INTERNAL; returns the document and its version 1, not yet scanned.</summary>
    public static async Task<(Guid DocumentId, Guid VersionId)> UploadAsync(this HttpClient client, string token, Guid projectId)
    {
        using HttpResponseMessage created = await client.UploadAsync(
            token, DocumentDriver.Text($"Handover certificate {Guid.NewGuid()}"), projectId, MilestoneTestHost.Internal, documentType: MilestoneTestHost.DocumentTypeId);
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"upload: {(int)created.StatusCode} {await created.Content.ReadAsStringAsync()}");
        JsonObject body = await created.ReadObjectAsync();
        return (AdministrationApi.IdOf(body), Guid.Parse(body["latestVersion"]!["id"]!.GetValue<string>()));
    }
}
