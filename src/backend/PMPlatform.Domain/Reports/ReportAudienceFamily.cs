namespace PMPlatform.Domain.Reports;

/// <summary>Which of the standard report experiences presents a report (FG-02 §5.2, SCR-131 to SCR-137; ERD <c>audience_family</c>).</summary>
public enum ReportAudienceFamily
{
    /// <summary>SCR-131 Executive Reports.</summary>
    Executive = 1,

    /// <summary>SCR-132 Portfolio Reports.</summary>
    Portfolio = 2,

    /// <summary>SCR-133 Department Reports.</summary>
    Department = 3,

    /// <summary>SCR-134 Project Reports.</summary>
    Project = 4,

    /// <summary>SCR-135 Financial Reports.</summary>
    Financial = 5,

    /// <summary>SCR-136 Progress Reports.</summary>
    Progress = 6,

    /// <summary>SCR-137 Risk &amp; Issue Reports.</summary>
    RiskIssue = 7,
}
