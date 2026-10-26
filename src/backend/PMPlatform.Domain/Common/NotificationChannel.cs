namespace PMPlatform.Domain.Common;

/// <summary>The three launch channels (ADR-004): FG-04 routes event families to them (TASK-034) and WF-15 delivers on them (TASK-039).</summary>
public enum NotificationChannel
{
    InApp = 1,
    Email = 2,
    Sms = 3,
}
