using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>Which project lifecycle states admit which FinancialKpi records (422 <c>FINANCIAL_KPI_PROJECT_NOT_ELIGIBLE</c>).</summary>
internal static class ProjectEligibility
{
    /// <summary>The budget, KPI assignments and targets are set while the project is approved and planned, and while it runs.</summary>
    public static AdministrationError? PlanningRefused(ProjectFacts project) =>
        project.Status is ProjectLifecycleState.ApprovedPlanned or ProjectLifecycleState.Active ? null : AdministrationError.Rule(FinancialKpiErrorCodes.ProjectNotEligible);

    /// <summary>Periodic figures — financial updates, KPI measurements — are reported while the project runs.</summary>
    public static AdministrationError? ReportingRefused(ProjectFacts project) =>
        project.Status == ProjectLifecycleState.Active ? null : AdministrationError.Rule(FinancialKpiErrorCodes.ProjectNotEligible);
}
