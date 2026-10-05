using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.Risk;

namespace PMPlatform.Application.Features.Risk;

/// <summary>
/// A risk's state machine (TASK-055): IDENTIFIED → ASSESSED by its first assessment; ASSESSED → TREATMENT or MONITORING;
/// TREATMENT ⇄ MONITORING; an acceptance takes the risk to MONITORING, and its revocation or expiry returns it to ASSESSED for
/// review; every open state → CLOSED; CLOSED → ASSESSED by a reopen, or IDENTIFIED for a risk never assessed. A reassessment
/// keeps the state. Migration <c>TASK-055_GuardRiskHistory</c> refuses every other change of status in the database too.
/// </summary>
internal static class RiskWorkflow
{
    public static IReadOnlySet<(RiskStatus From, RiskStatus To)> Transitions { get; } = new HashSet<(RiskStatus, RiskStatus)>
    {
        (RiskStatus.Identified, RiskStatus.Assessed),   // first assessment
        (RiskStatus.Assessed, RiskStatus.Treatment),    // start treatment
        (RiskStatus.Monitoring, RiskStatus.Treatment),
        (RiskStatus.Assessed, RiskStatus.Monitoring),   // monitor, or accept
        (RiskStatus.Treatment, RiskStatus.Monitoring),
        (RiskStatus.Monitoring, RiskStatus.Assessed),   // an acceptance revoked or expired: back for review
        (RiskStatus.Identified, RiskStatus.Closed),     // close
        (RiskStatus.Assessed, RiskStatus.Closed),
        (RiskStatus.Treatment, RiskStatus.Closed),
        (RiskStatus.Monitoring, RiskStatus.Closed),
        (RiskStatus.Closed, RiskStatus.Identified),     // reopen, RISK_REOPEN only
        (RiskStatus.Closed, RiskStatus.Assessed),
    };

    public static bool Allows(RiskStatus from, RiskStatus to) => from == to || Transitions.Contains((from, to));

    /// <summary>
    /// Where <paramref name="command"/> takes a risk in <paramref name="from"/>; null when the state machine has no such edge.
    /// A reopen returns the risk to review: ASSESSED once it has an assessment, IDENTIFIED otherwise.
    /// </summary>
    public static RiskStatus? TargetOf(RiskCommand command, RiskStatus from, bool assessed) => (command, from) switch
    {
        (RiskCommand.Assess, RiskStatus.Identified) => RiskStatus.Assessed,
        (RiskCommand.Assess, RiskStatus.Assessed or RiskStatus.Treatment or RiskStatus.Monitoring) => from,
        (RiskCommand.StartTreatment, RiskStatus.Assessed or RiskStatus.Monitoring) => RiskStatus.Treatment,
        (RiskCommand.Monitor, RiskStatus.Assessed or RiskStatus.Treatment) => RiskStatus.Monitoring,
        (RiskCommand.Accept, RiskStatus.Assessed or RiskStatus.Treatment or RiskStatus.Monitoring) => RiskStatus.Monitoring,
        (RiskCommand.RevokeAcceptance or RiskCommand.ExpireAcceptance, RiskStatus.Monitoring) => RiskStatus.Assessed,
        (RiskCommand.Close, RiskStatus.Identified or RiskStatus.Assessed or RiskStatus.Treatment or RiskStatus.Monitoring) => RiskStatus.Closed,
        (RiskCommand.Reopen, RiskStatus.Closed) => assessed ? RiskStatus.Assessed : RiskStatus.Identified,
        (RiskCommand.Materialise, RiskStatus.Identified or RiskStatus.Assessed or RiskStatus.Treatment or RiskStatus.Monitoring) => from,
        _ => null,
    };

    /// <summary>
    /// The permission each command is decided on. Rating and acceptance have their own, held by internal users only (ADR-013);
    /// the reopen has its own, so general edit permission never reopens a risk. An expiry is the service principal's, decided by
    /// the clock and no permission.
    /// </summary>
    public static string PermissionOf(RiskCommand command) => command switch
    {
        RiskCommand.Assess => PermissionCatalogue.RiskAssess,
        RiskCommand.StartTreatment or RiskCommand.Monitor or RiskCommand.Close or RiskCommand.Materialise => PermissionCatalogue.RiskManage,
        RiskCommand.Accept or RiskCommand.RevokeAcceptance => PermissionCatalogue.RiskAccept,
        RiskCommand.Reopen => PermissionCatalogue.RiskReopen,
        RiskCommand.ExpireAcceptance => throw new ArgumentOutOfRangeException(nameof(command), command, "An expiry is decided by the clock, not by a permission."),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown risk command."),
    };

    /// <summary>A risk still in the register's working set: every state but CLOSED.</summary>
    public static bool IsOpen(RiskStatus status) => status != RiskStatus.Closed;
}

/// <summary>The commands that move a risk along <see cref="RiskWorkflow"/>, one per edge family (api-conventions R-4).</summary>
internal enum RiskCommand
{
    Assess = 1,
    StartTreatment = 2,
    Monitor = 3,
    Accept = 4,
    RevokeAcceptance = 5,
    ExpireAcceptance = 6,
    Close = 7,
    Reopen = 8,
    Materialise = 9,
}
