namespace PMPlatform.Application.Features.AuditActivity.Contracts;

/// <summary>
/// The category an audit event is authorised by (FG-06 §6.1, AUD-CC-03; TBC-AUD-02). It is derived from the event's class and
/// the module it is about, never stored, so no event can be re-categorised. Each category has its own read permission.
/// </summary>
public enum AuditCategory
{
    /// <summary>Authentication attempts and authorization refusals.</summary>
    Security = 1,

    /// <summary>Privileged actions, permission changes and configuration changes.</summary>
    Administration = 2,

    /// <summary>Material integration operator actions (ADM-053).</summary>
    Integration = 3,

    /// <summary>The business events of WF-14 Financial / KPI.</summary>
    Financial = 4,

    /// <summary>The business events of WF-13 External Participation.</summary>
    External = 5,

    /// <summary>Every other lifecycle transition, data change and approval decision.</summary>
    Business = 6,
}
