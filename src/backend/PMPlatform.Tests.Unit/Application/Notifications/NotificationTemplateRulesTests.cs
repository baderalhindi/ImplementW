using PMPlatform.Application.Features.Notifications;
using PMPlatform.Domain.Common;

namespace PMPlatform.Tests.Unit.Application.Notifications;

/// <summary>ADR-012 (bilingual, mandatory) and ADR-004 (SMS: event and deep link only, within the segment budget) on a template's text.</summary>
public sealed class NotificationTemplateRulesTests
{
    private static readonly NotificationDeliveryPolicy Policy = new();

    [Fact]
    public void AnEmailNeedsASubjectInBothLanguages() =>
        Assert.Equal(["subjectAr REQUIRED", "subjectEn REQUIRED"], Issues(NotificationChannel.Email, null, " ", "نص", "Body"));

    [Fact]
    public void AnSmsHasNoSubject() =>
        Assert.Equal(["subjectEn NOT_ALLOWED"], Issues(NotificationChannel.Sms, null, "Subject", "رابط {{deepLink}}", "Link {{deepLink}}"));

    [Theory]
    [InlineData("Link", "SMS_CONTENT_RESTRICTED")]
    [InlineData("{{reportName}} {{deepLink}}", "SMS_CONTENT_RESTRICTED")]
    [InlineData("Link {{deepLink}", "MALFORMED_PLACEHOLDER")]
    public void AnSmsCarriesTheDeepLinkAndNothingElse(string bodyEn, string code) =>
        Assert.Equal([$"bodyEn {code}"], Issues(NotificationChannel.Sms, null, null, "رابط {{deepLink}}", bodyEn));

    /// <summary>Three UCS-2 segments are 201 characters; the link is counted at 130, and the separating space is one more.</summary>
    [Fact]
    public void AnArabicSmsFitsTheBudgetWithItsLinkOrIsRefused()
    {
        Assert.Empty(Issues(NotificationChannel.Sms, null, null, new string('ت', 70) + " {{deepLink}}", "Link {{deepLink}}"));
        Assert.Equal(["bodyAr SMS_SEGMENT_BUDGET_EXCEEDED"], Issues(NotificationChannel.Sms, null, null, new string('ت', 71) + " {{deepLink}}", "Link {{deepLink}}"));
    }

    [Fact]
    public void InAppAndEmailMayUseTheSourcesParameters() =>
        Assert.Empty(Issues(NotificationChannel.InApp, "تصعيد {{ref}}", "Escalated {{ref}}", "تم تصعيد {{ref}}", "{{ref}} was escalated: {{deepLink}}"));

    private static string[] Issues(NotificationChannel channel, string? subjectAr, string? subjectEn, string bodyAr, string bodyEn) =>
        [.. NotificationTemplateRules.Check(channel, subjectAr, subjectEn, bodyAr, bodyEn, Policy).Select(i => $"{i.Field} {i.Code}")];
}
