using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// TASK-052's rule for every figure: a value is present exactly when its status is MEASURED. A missing, stale or not-applicable
/// figure is null with its status saying why; it is never stored, nor shown, as 0.
/// </summary>
internal static class ValueRules
{
    /// <summary>An update's figures: the actual present exactly when MEASURED, the forecast only beside it, neither negative.</summary>
    public static AdministrationError? FinancialRefused(Money? actual, Money? forecast, ValueStatus status, DateOnly asOfDate, DateOnly today) =>
        !IsPresentExactlyWhenMeasured(status, actual is not null) ? AdministrationError.Rule(FinancialKpiErrorCodes.ValueStatusInvalid, new FieldIssue("actualExpenditureToDateSar", FieldIssue.NotAllowed))
        : forecast is not null && actual is null ? AdministrationError.Rule(FinancialKpiErrorCodes.ValueStatusInvalid, new FieldIssue("forecastAtCompletionSar", FieldIssue.NotAllowed))
        : AsOfRefused(asOfDate, today);

    /// <summary>A measurement's value: present exactly when MEASURED.</summary>
    public static AdministrationError? MeasurementRefused(decimal? value, ValueStatus status, DateOnly asOfDate, DateOnly today) =>
        !IsPresentExactlyWhenMeasured(status, value is not null) ? AdministrationError.Rule(FinancialKpiErrorCodes.ValueStatusInvalid, new FieldIssue("measuredValue", FieldIssue.NotAllowed))
        : AsOfRefused(asOfDate, today);

    /// <summary>A figure is true as of a day that has happened (ADR-008 provenance).</summary>
    public static AdministrationError? AsOfRefused(DateOnly asOfDate, DateOnly today) =>
        asOfDate > today ? AdministrationError.Rule(FinancialKpiErrorCodes.AsOfDateInvalid, new FieldIssue("asOfDate", FieldIssue.NotAllowed)) : null;

    private static bool IsPresentExactlyWhenMeasured(ValueStatus status, bool present) => status == ValueStatus.Measured ? present : !present;

    /// <summary>Dates are UTC calendar dates (progress-update.md F-7).</summary>
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);
}
