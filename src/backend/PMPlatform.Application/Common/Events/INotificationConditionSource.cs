namespace PMPlatform.Application.Common.Events;

/// <summary>
/// How Notifications revalidates a reminder's condition before sending it (TASK-039; event-conventions EV-10). A source
/// module that schedules reminders implements it for its own subjects; Notifications chooses the source by the intent's
/// <c>sourceModule</c>. The seam is in <c>Application/Common</c>, so neither module references the other and ADR-003
/// §8.2 gains no Notifications → source edge (event-conventions S-7).
/// </summary>
public interface INotificationConditionSource
{
    /// <summary>The module whose subjects this source answers for, as the envelope's <c>sourceModule</c> names it.</summary>
    public string SourceModule { get; }

    /// <summary>
    /// The subject's current status in upper snake case, as the module's own state column holds it; null when there is no
    /// such subject. Read now, never from a cache: a status that changed a moment ago must suppress the reminder.
    /// </summary>
    public Task<string?> FindStatusAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken);
}
