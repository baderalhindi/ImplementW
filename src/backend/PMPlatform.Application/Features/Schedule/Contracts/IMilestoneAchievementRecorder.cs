namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 10, WF-05 → WF-03 command (ICD-04): WF-05 has accepted an achievement of the milestone, and WF-03 — the
/// owner of the milestone's status — records it ACHIEVED. The status is the only thing WF-03 learns: the accepted Actual
/// Achievement Date stays WF-05's.
/// </summary>
public interface IMilestoneAchievementRecorder
{
    /// <summary>
    /// Under the project's schedule lock, stages PLANNED → ACHIEVED in the caller's unit of work, as the final WF-11 approver
    /// <paramref name="actorId"/> and naming the accepted revision on the audit event; the caller's save commits it with the
    /// acceptance, or neither commits (M-11). An ACHIEVED milestone — a correction accepted — is left as it is. False, with
    /// nothing staged, when the milestone is CANCELLED: no achievement of it can be accepted.
    /// </summary>
    public Task<bool> RecordAchievedAsync(Guid projectMilestoneId, Guid actorId, Guid milestoneAchievementId, CancellationToken cancellationToken);
}
