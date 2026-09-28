namespace PMPlatform.Application.Features.Approval;

public enum ApprovalSaveOutcome
{
    Saved = 1,

    /// <summary>Another decision on the same run was saved first.</summary>
    ConcurrencyConflict = 2,

    /// <summary>The subject revision already has a run: another start committed first.</summary>
    DuplicateRun = 3,
}
