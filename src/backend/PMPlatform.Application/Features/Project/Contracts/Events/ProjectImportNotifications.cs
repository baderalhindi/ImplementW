using PMPlatform.Application.Common.Events;

namespace PMPlatform.Application.Features.Project.Contracts.Events;

/// <summary>
/// <c>Project.ImportValidated</c>: the NOTIFICATION_INTENT a background validation publishes to its importer when it is done (the 2 Nov 2026
/// review §4, step 3). Addressed to that one person (parameter <c>recipientUserId</c>) among the PROJECT_IMPORT_VALIDATED family's roles.
/// </summary>
public sealed record ProjectImportValidated : EventEnvelope<NotificationIntentData>
{
    public const string Type = "Project.ImportValidated";

    public const int CurrentSchemaVersion = 1;

    public const string EventFamilyCode = "PROJECT_IMPORT_VALIDATED";
}

/// <summary><c>Project.ImportValueRequested</c>: an importer asks the administrators to add a value the platform's lists lack (review §3).</summary>
public sealed record ProjectImportValueRequested : EventEnvelope<NotificationIntentData>
{
    public const string Type = "Project.ImportValueRequested";

    public const int CurrentSchemaVersion = 1;

    public const string EventFamilyCode = "PROJECT_IMPORT_VALUE_REQUESTED";
}

/// <summary><c>Project.ImportValueResolved</c>: the requester is told the value was added, or why it was declined.</summary>
public sealed record ProjectImportValueResolved : EventEnvelope<NotificationIntentData>
{
    public const string Type = "Project.ImportValueResolved";

    public const int CurrentSchemaVersion = 1;

    public const string EventFamilyCode = "PROJECT_IMPORT_VALUE_RESOLVED";
}
