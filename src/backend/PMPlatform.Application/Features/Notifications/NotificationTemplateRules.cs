using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Notifications;

/// <summary>
/// What a template's text must be (ADR-012, ADR-004). Both languages always. E-mail and in-app have a subject in both;
/// SMS has none. Placeholders are well formed. An SMS carries the event and its deep link only: its only placeholder is
/// <c>{{deepLink}}</c>, which it must contain, and in each language it fits the segment budget with the link counted at
/// its allowance — Arabic in UCS-2, 70 characters a segment.
/// </summary>
internal static class NotificationTemplateRules
{
    public static IReadOnlyList<FieldIssue> Check(NotificationChannel channel, string? subjectAr, string? subjectEn, string bodyAr, string bodyEn, NotificationDeliveryPolicy policy)
    {
        List<FieldIssue> issues = [];
        bool sms = channel == NotificationChannel.Sms;
        CheckSubject(subjectAr, "subjectAr", sms, issues);
        CheckSubject(subjectEn, "subjectEn", sms, issues);
        CheckBody(bodyAr, "bodyAr", sms, policy, issues);
        CheckBody(bodyEn, "bodyEn", sms, policy, issues);
        return issues;
    }

    private static void CheckSubject(string? subject, string field, bool sms, List<FieldIssue> issues)
    {
        if (sms)
        {
            if (subject is not null)
            {
                issues.Add(new FieldIssue(field, FieldIssue.NotAllowed));
            }
        }
        else if (string.IsNullOrWhiteSpace(subject))
        {
            issues.Add(new FieldIssue(field, FieldIssue.Required));
        }
        else if (!TemplateText.IsWellFormed(subject))
        {
            issues.Add(new FieldIssue(field, NotificationIssueCodes.MalformedPlaceholder));
        }
    }

    private static void CheckBody(string body, string field, bool sms, NotificationDeliveryPolicy policy, List<FieldIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            issues.Add(new FieldIssue(field, FieldIssue.Required));
            return;
        }

        if (!TemplateText.IsWellFormed(body))
        {
            issues.Add(new FieldIssue(field, NotificationIssueCodes.MalformedPlaceholder));
            return;
        }

        if (!sms)
        {
            return;
        }

        IReadOnlyList<string> placeholders = TemplateText.Placeholders(body);
        if (placeholders is not [TemplateText.DeepLink])
        {
            issues.Add(new FieldIssue(field, NotificationIssueCodes.SmsContentRestricted));
            return;
        }

        string longest = TemplateText.Render(body, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TemplateText.DeepLink] = new string('x', policy.SmsDeepLinkAllowance),
        })!;
        if (SmsSegments.Count(longest) > policy.SmsMaxSegments)
        {
            issues.Add(new FieldIssue(field, NotificationIssueCodes.SmsSegmentBudgetExceeded));
        }
    }
}
