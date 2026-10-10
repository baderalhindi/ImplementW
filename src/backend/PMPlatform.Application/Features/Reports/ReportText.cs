using PMPlatform.Application.Common.Projections;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// The wording of a generated output in Arabic and English (ADR-012; US-RPT-EXE-018): headings, the words for a value that is not there, and the
/// names of the source-owned states a report presents. The API returns codes and the SPA words them; a file is read without the SPA, so it
/// carries its own words. A code with no wording here is written as its code, never dropped.
/// </summary>
internal static class ReportText
{
    public static readonly BilingualLabel GeneratedAt = new("تاريخ الإعداد", "Generated at");
    public static readonly BilingualLabel Version = new("الإصدار", "Version");
    public static readonly BilingualLabel Parameters = new("المعايير", "Parameters");
    public static readonly BilingualLabel Filters = new("عوامل التصفية", "Filters");
    public static readonly BilingualLabel Sources = new("مصادر البيانات", "Sources");
    public static readonly BilingualLabel Classification = new("التصنيف", "Classification");
    public static readonly BilingualLabel Rows = new("عدد الصفوف", "Rows");
    public static readonly BilingualLabel Page = new("صفحة", "Page");
    public static readonly BilingualLabel Of = new("من", "of");
    public static readonly BilingualLabel AsOf = new("حتى", "as of");
    public static readonly BilingualLabel Coverage = new("التغطية", "coverage");
    public static readonly BilingualLabel Explorer = new("مستكشف التقارير", "Report explorer");
    public static readonly BilingualLabel StaleNote = new(
        "* قيمة متقادمة: آخر قيمة لدى المصدر، معروضة بتاريخها؛ انظر مصادر البيانات.",
        "* Stale: the source's last value, shown with its own as-of; see Sources.");

    public static readonly BilingualLabel NoRows = new("لا توجد صفوف مصرح بها لهذه المعايير.", "No authorised rows for these parameters.");

    private static readonly BilingualLabel Standard = new("عادي", "Standard");

    private static readonly BilingualLabel Sensitive = new("حساس — يتضمن بيانات مالية أو مصنفة", "Sensitive — holds financial or classified data");

    private static readonly Dictionary<ReportUnknownReason, BilingualLabel> Reasons = new()
    {
        [ReportUnknownReason.Missing] = new("لا توجد بيانات", "Missing"),
        [ReportUnknownReason.NotApplicable] = new("لا ينطبق", "Not applicable"),
        [ReportUnknownReason.Restricted] = new("مقيد", "Restricted"),
        [ReportUnknownReason.SourceUnavailable] = new("المصدر غير متاح", "Source unavailable"),
    };

    private static readonly Dictionary<ProjectionSemanticState, BilingualLabel> States = new()
    {
        [ProjectionSemanticState.CurrentLive] = new("حالي", "Current"),
        [ProjectionSemanticState.PublishedOfficial] = new("منشور رسمي", "Published"),
        [ProjectionSemanticState.HistoricalSnapshot] = new("تاريخي", "Historical"),
    };

    private static readonly Dictionary<ProjectionFreshness, BilingualLabel> Freshness = new()
    {
        [ProjectionFreshness.Fresh] = new("محدث", "fresh"),
        [ProjectionFreshness.Stale] = new("متقادم", "stale"),
        [ProjectionFreshness.Unknown] = new("غير معروف", "unknown"),
    };

    private static readonly Dictionary<string, BilingualLabel> Codes = new(StringComparer.Ordinal)
    {
        ["GREEN"] = new("أخضر", "Green"),
        ["AMBER"] = new("كهرماني", "Amber"),
        ["RED"] = new("أحمر", "Red"),
        ["UNKNOWN"] = new("غير معروف", "Unknown"),
        ["NOT_APPLICABLE"] = new("لا ينطبق", "Not applicable"),
        ["DRAFT"] = new("مسودة", "Draft"),
        ["SUBMITTED"] = new("مقدم", "Submitted"),
        ["UNDER_REVIEW"] = new("قيد المراجعة", "Under review"),
        ["RETURNED"] = new("معاد", "Returned"),
        ["APPROVED_PLANNED"] = new("معتمد ومخطط", "Approved, planned"),
        ["ACTIVE"] = new("نشط", "Active"),
        ["SUSPENDED"] = new("معلق", "Suspended"),
        ["COMPLETED"] = new("مكتمل", "Completed"),
        ["CLOSED"] = new("مغلق", "Closed"),
        ["UP_TO_DATE"] = new("محدث", "Up to date"),
        ["OVERDUE"] = new("متأخر", "Overdue"),
        ["true"] = new("نعم", "Yes"),
        ["false"] = new("لا", "No"),
    };

    public static string In(BilingualLabel label, Language language)
    {
        ArgumentNullException.ThrowIfNull(label);
        return language == Language.Ar ? label.Ar : label.En;
    }

    public static string Reason(ReportUnknownReason reason, Language language) => In(Reasons[reason], language);

    /// <summary>The reason's language-neutral code, as the API names it (R-19): what a data extract holds for no value.</summary>
    public static string ReasonCode(ReportUnknownReason reason) => System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(reason.ToString());

    public static string State(ProjectionSemanticState state, Language language) => In(States[state], language);

    public static string Fresh(ProjectionFreshness freshness, Language language) => In(Freshness[freshness], language);

    public static string Sensitivity(bool sensitive, Language language) => In(sensitive ? Sensitive : Standard, language);

    /// <summary>A source-owned code in words; a code without wording here is written as itself.</summary>
    public static string Code(string code, Language language) => Codes.TryGetValue(code, out BilingualLabel? label) ? In(label, language) : code;

    public static string Unit(ReportValueType type, Language language) => type switch
    {
        ReportValueType.Sar => language == Language.Ar ? " ر.س" : " SAR",
        ReportValueType.Percent => "%",
        ReportValueType.Days => language == Language.Ar ? " يوم" : " days",
        ReportValueType.Code or ReportValueType.Text or ReportValueType.Reference or ReportValueType.Count or ReportValueType.Boolean
            or ReportValueType.Date or ReportValueType.DateTime => string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown value type."),
    };
}
