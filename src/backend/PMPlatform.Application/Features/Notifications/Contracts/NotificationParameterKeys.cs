namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary>Parameters WF-15 itself reads from an intent, beside those its templates render.</summary>
public static class NotificationParameterKeys
{
    /// <summary>
    /// An intent addressed to one person (the 2 Nov 2026 review: "a notification when done" to the importer): of the people the family's role
    /// matrix names over the intent's scope, only this user receives it; a user the matrix does not name receives nothing.
    /// </summary>
    public const string RecipientUserId = "recipientUserId";
}
