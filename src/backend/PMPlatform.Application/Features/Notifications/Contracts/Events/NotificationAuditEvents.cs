namespace PMPlatform.Application.Features.Notifications.Contracts.Events;

/// <summary>
/// The audit event types Notifications produces (TASK-039; event-conventions EV-1), recorded through <c>IAuditTrail</c> in
/// the producer's unit of work. The list is appended to event-conventions.md §4 with this task. Routing and delivery are
/// operational history, kept in the module's own tables, not audit events.
/// </summary>
public static class NotificationAuditEvents
{
    /// <summary>CONFIGURATION_CHANGE.</summary>
    public const string TemplateCreated = "Notifications.TemplateCreated";

    /// <summary>CONFIGURATION_CHANGE.</summary>
    public const string TemplateUpdated = "Notifications.TemplateUpdated";

    /// <summary>CONFIGURATION_CHANGE.</summary>
    public const string TemplateValidated = "Notifications.TemplateValidated";

    /// <summary>CONFIGURATION_CHANGE; the version it supersedes is retired in the same change.</summary>
    public const string TemplatePublished = "Notifications.TemplatePublished";

    /// <summary>CONFIGURATION_CHANGE.</summary>
    public const string TemplateRetired = "Notifications.TemplateRetired";

    /// <summary>DATA_CHANGE: a recipient turned a family on or off on a channel.</summary>
    public const string PreferenceChanged = "Notifications.PreferenceChanged";

    /// <summary>PRIVILEGED_ACTION: a FAILED intent was sent back to be routed again.</summary>
    public const string IntentRedriven = "Notifications.IntentRedriven";

    /// <summary>PRIVILEGED_ACTION: a dead-lettered delivery was sent back to be tried again.</summary>
    public const string DeliveryRedriven = "Notifications.DeliveryRedriven";
}
