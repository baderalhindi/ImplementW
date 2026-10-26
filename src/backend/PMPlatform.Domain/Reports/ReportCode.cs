namespace PMPlatform.Domain.Reports;

/// <summary>
/// The ten reports the platform delivers (ADR-006: "3 dashboards and 10 reports"; a report counts once whatever its formats, ADR-005). FG-02's
/// twenty-six catalogue entries are absorbed into them as parameters, options and audiences (ADR-006 MAPPED; reports.md D-2). Stored as its
/// name, so the database refuses an eleventh.
/// </summary>
public enum ReportCode
{
    /// <summary>RPT-PRJ-001 Project Portfolio Summary, with RPT-EXE-001 Executive Management Report and RPT-PRJ-004 Project Health.</summary>
    PortfolioSummary = 1,

    /// <summary>RPT-PRJ-002 Project Register, with RPT-PRJ-003 Lifecycle/Status, RPT-SUS-001 Suspended and RPT-CLO-001 Completed/Closed as lifecycle options.</summary>
    ProjectRegister = 2,

    /// <summary>RPT-PRJ-005 Project Detail / Project Summary: the formal report of one project.</summary>
    ProjectReport = 3,

    /// <summary>RPT-PRG-001 Progress Reporting Status.</summary>
    ProgressReporting = 4,

    /// <summary>RPT-PRG-002 Published Progress History: WF-02's own snapshots.</summary>
    ProgressHistory = 5,

    /// <summary>RPT-SCH-001 Schedule / Baseline / Forecast, with RPT-TSK-001 Task Register and RPT-MIL-001 Milestone Register.</summary>
    ScheduleDelivery = 6,

    /// <summary>RPT-RSK-001 Risk Register, RPT-RSK-002 Risk Exception / Exposure and RPT-ISS-001 Issue / Challenge Register.</summary>
    RiskIssue = 7,

    /// <summary>RPT-FIN-001 Financial Performance: sensitive (ADR-010).</summary>
    FinancialPerformance = 8,

    /// <summary>RPT-KPI-001 KPI Performance, with RPT-CMP-001 Financial / KPI Reporting Completeness.</summary>
    KpiPerformance = 9,

    /// <summary>RPT-CHG-001 Change Request Register, RPT-APR-001 Approval Workload, RPT-CLO-002 Post-Project Obligations and RPT-EXT-001 External Requests.</summary>
    GovernanceChange = 10,
}
