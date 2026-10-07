using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.ChangeRequest.Contracts;

/// <summary>
/// The ACTIVE Approved Baseline a change request is evaluated against and a REBASELINE authorisation pins (ADR-016; edge 11). A port
/// WF-08 owns and WF-03 implements, so the code reference runs Schedule → ChangeRequest only, as edge 11 does: ChangeRequest never
/// references Schedule.
/// </summary>
public interface IApprovedBaselineSource
{
    /// <summary>Null when the project has no ACTIVE baseline of type APPROVED: a Declared Baseline is not a governed rebaseline target.</summary>
    public Task<ApprovedBaselineFacts?> FindActiveAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <summary>An ACTIVE Approved Baseline: its identity and version, and the dates its duration is counted between, inclusive.</summary>
public sealed record ApprovedBaselineFacts(Guid BaselineId, int VersionNo, DateOnly StartDate, DateOnly FinishDate)
{
    /// <summary>Calendar days from start to finish, both included.</summary>
    public int DurationDays => FinishDate.DayNumber - StartDate.DayNumber + 1;
}

/// <summary>
/// The ACTIVE Approved Budget a change request's cost is evaluated against and a COMMITMENT_CHANGE authorisation pins (ADR-016;
/// edge 12). A port WF-08 owns and WF-14 implements, so ChangeRequest never references FinancialKpi.
/// </summary>
public interface IApprovedBudgetSource
{
    /// <summary>Null when the project has no ACTIVE APPROVED_BUDGET version.</summary>
    public Task<ApprovedBudgetFacts?> FindActiveAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <summary>An ACTIVE Approved Budget version: its identity, version and amount.</summary>
public sealed record ApprovedBudgetFacts(Guid CommitmentId, int VersionNo, Money AmountSar);
