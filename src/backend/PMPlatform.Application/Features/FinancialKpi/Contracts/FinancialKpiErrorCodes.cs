namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>The FinancialKpi module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class FinancialKpiErrorCodes
{
    /// <summary>
    /// 422: the project's lifecycle state admits no such record. The budget, KPI assignments and targets are planned on an
    /// APPROVED_PLANNED or ACTIVE project; periodic figures — financial updates and measurements — are reported on an ACTIVE one.
    /// </summary>
    public const string ProjectNotEligible = "FINANCIAL_KPI_PROJECT_NOT_ELIGIBLE";

    /// <summary>422: the field is INTEGRATED for this project, so no figure is entered by hand (ADR-008: no manual substitution).</summary>
    public const string FieldIntegrated = "FINANCIAL_FIELD_INTEGRATED";

    /// <summary>409: the field is INTEGRATED and never returns to MANUAL or HYBRID (ADR-008).</summary>
    public const string SourceModeLocked = "FINANCIAL_SOURCE_MODE_LOCKED";

    /// <summary>422: a commitment type or field that is not used at launch (OPEN_COMMITMENT), or one that no request writes (DECLARED_BUDGET).</summary>
    public const string NotInUse = "FINANCIAL_NOT_IN_USE";

    /// <summary>409: the project already has a commitment version on its way (DRAFT, SUBMITTED or RETURNED).</summary>
    public const string CommitmentOpen = "FINANCIAL_COMMITMENT_OPEN";

    /// <summary>409: only a DRAFT or RETURNED version is edited, and only a DRAFT is deleted or has its documents changed.</summary>
    public const string VersionNotEditable = "FINANCIAL_KPI_VERSION_NOT_EDITABLE";

    /// <summary>422: a change to the Approved Budget needs a referenced document: the version holds no CLEAN evidence (ADR-008 gate).</summary>
    public const string BudgetDocumentRequired = "FINANCIAL_BUDGET_DOCUMENT_REQUIRED";

    /// <summary>409: every reporting period that has begun has published financial figures, or WF-02 has generated none yet.</summary>
    public const string NothingToReport = "FINANCIAL_NOTHING_TO_REPORT";

    /// <summary>409: the period already has a revision of its financial update in progress.</summary>
    public const string UpdateExists = "FINANCIAL_UPDATE_EXISTS";

    /// <summary>409: only a DRAFT update or measurement is edited or deleted.</summary>
    public const string NotEditable = "FINANCIAL_KPI_NOT_EDITABLE";

    /// <summary>422: a figure is present exactly when its value status is MEASURED; a forecast only beside a measured actual.</summary>
    public const string ValueStatusInvalid = "FINANCIAL_KPI_VALUE_STATUS_INVALID";

    /// <summary>422: the as-of date is after today: a figure is true as of a day that has happened.</summary>
    public const string AsOfDateInvalid = "FINANCIAL_KPI_AS_OF_DATE_INVALID";

    /// <summary>422: the KPI definition is not PUBLISHED, or the frequency is not a PUBLISHED MEASUREMENT_FREQUENCY item.</summary>
    public const string KpiReferenceInvalid = "KPI_REFERENCE_INVALID";

    /// <summary>409: the KPI is already assigned to the project.</summary>
    public const string KpiAlreadyAssigned = "KPI_ALREADY_ASSIGNED";

    /// <summary>422: target versions and measurements are recorded on an ACTIVE assignment only.</summary>
    public const string AssignmentNotActive = "KPI_ASSIGNMENT_NOT_ACTIVE";

    /// <summary>409: the assignment already has a target version on its way (DRAFT, SUBMITTED or RETURNED).</summary>
    public const string TargetOpen = "KPI_TARGET_OPEN";

    /// <summary>422: the thresholds do not order as the KPI's direction requires, or only one of the two is given.</summary>
    public const string ThresholdsInvalid = "KPI_THRESHOLDS_INVALID";

    /// <summary>422: the assignment has no ACTIVE target version to pin a measurement to.</summary>
    public const string TargetNotApproved = "KPI_TARGET_NOT_APPROVED";

    /// <summary>409: the assignment already has a measurement for the period.</summary>
    public const string MeasurementExists = "KPI_MEASUREMENT_EXISTS";

    /// <summary>422: the period ends before it starts.</summary>
    public const string PeriodInvalid = "KPI_PERIOD_INVALID";
}
