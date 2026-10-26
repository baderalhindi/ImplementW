namespace PMPlatform.Domain.Dashboards;

/// <summary>
/// ERD <c>dashboard_definition.code</c>: the three delivered dashboards (ADR-006, "FIXED at 3 dashboards"). FG-01's twelve DSH
/// definitions are absorbed into them as audience, scope and permission renderings, not separate deliverables, so a fourth code
/// is a contract change and the column's CHECK refuses it.
/// </summary>
public enum DashboardCode
{
    /// <summary>DSH-002 Portfolio, with DSH-003 Department, DSH-006 Viewer and DSH-007 Executive as scope renderings, the role-aware Home of those roles, and the summary widgets of DSH-010 to DSH-012.</summary>
    Portfolio = 1,

    /// <summary>DSH-009 Project, composed into SCR-040, with DSH-008 External Contributor as its ENTITY-scoped permission rendering (ADR-006, ADR-013).</summary>
    Project = 2,

    /// <summary>DSH-001 Administration, DSH-004 Project Manager and DSH-005 Liaison: obligations, reporting completeness and configuration attention (task record D-2).</summary>
    Governance = 3,
}
