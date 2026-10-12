using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Domain.Common;
using PMPlatform.Infrastructure.Audit;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.AuditActivity;

/// <summary>
/// TASK-073's tamper evidence (AUD-CC-27; FG-06 §24.2 "Integrity"): the whole chain verifies, and evidence changed out of band — by
/// the table's owner, who alone can switch the protection off (TASK-033 F-4) — is found and reported, never repaired. Operating the
/// store is its own permission.
/// </summary>
[Collection(AuditActivitySuite.Name)]
public sealed class AuditIntegrityTests(AuditActivityTestHost host)
{
    [Fact]
    public async Task TheChainVerifiesAndTheVerificationIsAudited()
    {
        await host.StageAsync(AuditActivityTestHost.Business("Risk.RiskClosed", AuditActivityTestHost.ProjectA, Guid.NewGuid(), 2));
        using HttpClient operator1 = await host.ClientAsync(1);

        JsonObject report = await (await operator1.PostSensitiveAsync(AuditActivityApi.Verifications)).OkAsync();
        int command = await host.Api.Services.RunAuditCommandAsync(AuditCommands.VerifyIntegrityCommand);

        Assert.True(report["intact"]!.GetValue<bool>(), report.ToJsonString());
        Assert.True(report["eventsVerified"]!.GetValue<int>() > 0);
        Assert.Equal(0, report["eventsWithoutDigest"]!.GetValue<int>());
        Assert.Equal(0, command);
        Assert.Contains($"SUCCESS|{IdentityDatabase.UserId(1)}", await VerificationsAsync());
    }

    [Fact]
    public async Task AnAttributeChangedOutOfBandIsFound()
    {
        Guid subject = Guid.NewGuid();
        await host.StageAsync(AuditActivityTestHost.Business("Risk.RiskReopened", AuditActivityTestHost.ProjectA, subject, 2));
        Guid eventId = await host.EventIdAsync(subject);
        using HttpClient operator1 = await host.ClientAsync(1);

        await TamperAsync(eventId, "TAMPERED");
        try
        {
            JsonObject report = await (await operator1.PostSensitiveAsync(AuditActivityApi.Verifications)).OkAsync();
            int command = await host.Api.Services.RunAuditCommandAsync(AuditCommands.VerifyIntegrityCommand);

            Assert.False(report["intact"]!.GetValue<bool>());
            Assert.Equal((0, 1), (report["brokenLinks"]!.GetValue<int>(), report["attributeDigestMismatches"]!.GetValue<int>()));
            Assert.Equal($"{eventId}|ATTRIBUTE_DIGEST_MISMATCH", Assert.Single(report["findings"]!.AsArray().Select(f => $"{f!["auditEventId"]}|{f["kind"]}")));
            Assert.Equal(1, command);
            Assert.Contains($"FAILED|{IdentityDatabase.UserId(1)}", await VerificationsAsync());
        }
        finally
        {
            await TamperAsync(eventId, "CHANGED");
        }

        JsonObject restored = await (await operator1.PostSensitiveAsync(AuditActivityApi.Verifications)).OkAsync();
        Assert.True(restored["intact"]!.GetValue<bool>());
    }

    [Fact]
    public async Task TheIngestionStatusShowsTheStoreAndTheProjectionUpToDate()
    {
        await host.StageAsync(AuditActivityTestHost.Business("Risk.TreatmentStarted", AuditActivityTestHost.ProjectA, Guid.NewGuid(), 2));
        using HttpClient operator1 = await host.ClientAsync(1);

        JsonObject status = await (await operator1.GetAsync(AuditActivityApi.Status)).OkAsync();

        Assert.True(status["eventsStored"]!.GetValue<int>() > 0);
        Assert.Equal(status["storeHeadRecordedAt"]!.GetValue<DateTimeOffset>(), status["projectionCheckpointRecordedAt"]!.GetValue<DateTimeOffset>());
        Assert.Equal(0, status["projectionLag"]!.GetValue<int>());
    }

    /// <summary>Holding every audit category is not operating the store: verification, checks, rebuilds and status are AUDIT_OPERATIONS_MANAGE.</summary>
    [Fact]
    public async Task OnlyAnOperatorOperatesTheStore()
    {
        using HttpClient reviewer = await host.ClientAsync(2);

        using HttpResponseMessage verify = await reviewer.PostSensitiveAsync(AuditActivityApi.Verifications);
        using HttpResponseMessage check = await reviewer.PostSensitiveAsync(AuditActivityApi.ProjectionChecks);
        using HttpResponseMessage rebuild = await reviewer.PostSensitiveAsync(AuditActivityApi.ProjectionRebuilds);
        using HttpResponseMessage status = await reviewer.GetAsync(AuditActivityApi.Status);

        Assert.All([verify, check, rebuild, status], response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
    }

    /// <summary>Changes an attribute as the table's owner can: with the append-only trigger switched off for the statement's transaction.</summary>
    private Task TamperAsync(Guid eventId, string value) => host.Database.ExecuteAsync($"""
        BEGIN;
        ALTER TABLE audit_activity.audit_event_attribute DISABLE TRIGGER append_only;
        UPDATE audit_activity.audit_event_attribute SET new_value = '{value}' WHERE audit_event_id = '{eventId}' AND attribute_name = 'status';
        ALTER TABLE audit_activity.audit_event_attribute ENABLE TRIGGER append_only;
        COMMIT;
        """);

    private Task<IReadOnlyList<string>> VerificationsAsync() => host.Database.QueryAsync(
        $"SELECT DISTINCT outcome || '|' || actor_user_id FROM audit_activity.audit_event WHERE event_type = 'AuditActivity.IntegrityVerified' AND actor_type = '{nameof(AuditActorType.User).ToUpperInvariant()}'");
}
