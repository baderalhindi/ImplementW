using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>
/// Bilingual, mandatory templates (ADR-012) under the governed lifecycle: three people, the content rules — an SMS carries
/// the event and its deep link only and fits the Arabic segment budget (ADR-004) — and publication that retires what it replaces.
/// </summary>
[Collection(NotificationSuite.Name)]
public sealed class NotificationTemplateEndpointTests(NotificationTestHost host)
{
    private const string Templates = "/api/v1/notification-templates";

    [Theory]
    [InlineData("SMS", null, null, "تقرير جاهز: {{deepLink}}", "Report {{reportName}} is ready: {{deepLink}}", "bodyEn SMS_CONTENT_RESTRICTED")]
    [InlineData("SMS", null, null, "تقرير جاهز", "A report is ready: {{deepLink}}", "bodyAr SMS_CONTENT_RESTRICTED")]
    [InlineData("SMS", "موضوع", "Subject", "تقرير جاهز: {{deepLink}}", "A report is ready: {{deepLink}}", "subjectAr NOT_ALLOWED,subjectEn NOT_ALLOWED")]
    [InlineData("EMAIL", null, "Subject", "نص", "Body", "subjectAr REQUIRED")]
    [InlineData("IN_APP", "موضوع", "Subject {{reportName", "نص", "Body", "subjectEn MALFORMED_PLACEHOLDER")]
    public async Task AContentRuleBrokenIsRefusedWithItsField(string channel, string? subjectAr, string? subjectEn, string bodyAr, string bodyEn, string fields)
    {
        using HttpClient client = host.Api.CreateClient();
        string author = (await client.SignInOrFailAsync(1)).AccessToken;

        using HttpResponseMessage response = await client.PostAsync(Templates, author,
            new { eventFamilyCode = NotificationTestHost.ReportFamily, eventType = "Reports.ReportJobRuleCase", channel, subjectAr, subjectEn, bodyAr, bodyEn });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        JsonObject problem = await response.ReadObjectAsync();
        Assert.Equal("NOTIFICATION_TEMPLATE_INVALID", problem["code"]!.GetValue<string>());
        Assert.Equal(fields, string.Join(',', problem["errors"]!.AsArray().Select(e => $"{e!["field"]} {e["code"]}")));
    }

    /// <summary>ADR-004's Arabic budget: 3 UCS-2 segments of 67 with the link counted at 130 characters leave 71 for the text.</summary>
    [Fact]
    public async Task AnArabicSmsOverTheSegmentBudgetIsRefused()
    {
        using HttpClient client = host.Api.CreateClient();
        string author = (await client.SignInOrFailAsync(1)).AccessToken;
        string fits = new string('ت', 70) + " {{deepLink}}";
        string over = new string('ت', 71) + " {{deepLink}}";

        using HttpResponseMessage refused = await client.PostAsync(Templates, author,
            new { eventFamilyCode = NotificationTestHost.ReportFamily, eventType = "Reports.ReportJobBudget", channel = "SMS", bodyAr = over, bodyEn = "Ready: {{deepLink}}" });
        using HttpResponseMessage accepted = await client.PostAsync(Templates, author,
            new { eventFamilyCode = NotificationTestHost.ReportFamily, eventType = "Reports.ReportJobBudget", channel = "SMS", bodyAr = fits, bodyEn = "Ready: {{deepLink}}" });

        Assert.Equal(["bodyAr SMS_SEGMENT_BUDGET_EXCEEDED"], await refused.ReadFieldErrorsAsync());
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task ThreePeopleAuthorValidateAndPublishAndANewVersionRetiresTheOld()
    {
        using HttpClient client = host.Api.CreateClient();
        string r01 = (await client.SignInOrFailAsync(1)).AccessToken;
        string r02 = (await client.SignInOrFailAsync(2)).AccessToken;
        string r03 = (await client.SignInOrFailAsync(3)).AccessToken;
        string eventType = $"Reports.ReportJobQueued{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        Guid first = await PublishAsync(client, r01, r02, r03, eventType, "Report {{reportName}} is queued.");
        using HttpResponseMessage created = await client.PostAsync(Templates, r01, Draft(eventType, "Report {{reportName}} is queued for you."));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonObject second = await created.ReadObjectAsync();
        Assert.Equal(2, second["versionNo"]!.GetValue<int>());
        Assert.Equal($"{Templates}/{second["id"]}", created.Headers.Location!.ToString());
        string path = $"{Templates}/{second["id"]}";

        // Only the author edits a draft, and only with If-Match.
        using (HttpResponseMessage noIfMatch = await client.PutAsync(path, r01, Text("Report {{reportName}} waits."), ifMatch: null))
        {
            Assert.Equal(HttpStatusCode.PreconditionRequired, noIfMatch.StatusCode);
        }

        using (HttpResponseMessage notAuthor = await client.PutAsync(path, r02, Text("Report {{reportName}} waits."), AdministrationApi.ETagOf(created)))
        {
            Assert.Equal("NOTIFICATION_SEPARATION_OF_DUTIES", (await notAuthor.ReadObjectAsync())["code"]!.GetValue<string>());
        }

        using (HttpResponseMessage ownValidation = await client.PostAsync($"{path}/validate", r01))
        {
            Assert.Equal("NOTIFICATION_SEPARATION_OF_DUTIES", (await ownValidation.ReadObjectAsync())["code"]!.GetValue<string>());
        }

        using (HttpResponseMessage validated = await client.PostAsync($"{path}/validate", r02))
        {
            Assert.Equal(HttpStatusCode.OK, validated.StatusCode);
        }

        using (HttpResponseMessage reviewerPublishes = await client.PostAsync($"{path}/publish", r02))
        {
            Assert.Equal("NOTIFICATION_SEPARATION_OF_DUTIES", (await reviewerPublishes.ReadObjectAsync())["code"]!.GetValue<string>());
        }

        using (HttpResponseMessage published = await client.PostAsync($"{path}/publish", r03))
        {
            Assert.Equal("PUBLISHED", (await published.ReadObjectAsync())["lifecycleState"]!.GetValue<string>());
        }

        Assert.Equal(["1|RETIRED", "2|PUBLISHED"], await host.Database.QueryAsync(
            $"SELECT version_no || '|' || lifecycle_state FROM notifications.notification_template WHERE event_type = '{eventType}' ORDER BY version_no"));
        Assert.Equal(["Notifications.TemplatePublished", "Notifications.TemplateRetired"], await host.Database.QueryAsync($"""
            SELECT event_type FROM audit_activity.audit_event
            WHERE event_type IN ('Notifications.TemplatePublished', 'Notifications.TemplateRetired') AND subject_id IN ('{first}', '{second["id"]}')
              AND occurred_at > (SELECT validated_at FROM notifications.notification_template WHERE id = '{second["id"]}')
            ORDER BY event_type
            """));

        // A published template takes no edit, and the database refuses one that goes round the application.
        using HttpResponseMessage current = await client.GetAsync(path, r01);
        using (HttpResponseMessage edit = await client.PutAsync(path, r01, Text("Changed."), AdministrationApi.ETagOf(current)))
        {
            Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        }

        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => host.Database.ExecuteAsync(
            $"UPDATE notifications.notification_template SET body_en = 'Changed.' WHERE id = '{second["id"]}'"));

        // Nobody without the permission reads the templates.
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;
        using HttpResponseMessage forbidden = await client.GetAsync(Templates, viewer);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    private static async Task<Guid> PublishAsync(HttpClient client, string author, string reviewer, string publisher, string eventType, string bodyEn)
    {
        using HttpResponseMessage created = await client.PostAsync(Templates, author, Draft(eventType, bodyEn));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Guid id = AdministrationApi.IdOf(await created.ReadObjectAsync());
        using HttpResponseMessage validated = await client.PostAsync($"{Templates}/{id}/validate", reviewer);
        Assert.Equal(HttpStatusCode.OK, validated.StatusCode);
        using HttpResponseMessage published = await client.PostAsync($"{Templates}/{id}/publish", publisher);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        return id;
    }

    private static object Draft(string eventType, string bodyEn) => new
    {
        eventFamilyCode = NotificationTestHost.ReportFamily,
        eventType,
        channel = "IN_APP",
        subjectAr = "تقرير",
        subjectEn = "Report",
        bodyAr = "التقرير {{reportName}} في الانتظار.",
        bodyEn,
    };

    private static object Text(string bodyEn) => new { subjectAr = "تقرير", subjectEn = "Report", bodyAr = "التقرير {{reportName}}.", bodyEn };
}
