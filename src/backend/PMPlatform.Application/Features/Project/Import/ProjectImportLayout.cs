using PMPlatform.Application.Common.DataImport;

namespace PMPlatform.Application.Features.Project.Import;

/// <summary>
/// The columns of RPMO's import workbook (RPMO_Projects_Import_v1, the 2 Nov 2026 review §3): each column is found by the field key in the
/// key row, never by position, so a column added or moved in RPMO's sheet is read the same. Columns the platform does not hold — the entity
/// type, the old status, the project type, the file's own bookkeeping — are skipped with an info note (the 5 Nov 2026 review §4.2).
/// </summary>
public static class ProjectImportLayout
{
    public const string DataSheet = "بيانات المشاريع";

    /// <summary>The template the platform hands out today. An uploaded template stamped with an older version is flagged.</summary>
    public const int TemplateVersion = 1;

    /// <summary>The key row's cell that carries the stamp, in a hidden column: <c>template_version=1</c>.</summary>
    public const string VersionKeyPrefix = "template_version=";

    public const int MaxRows = 1_500;

    /// <summary>A workbook of more rows than this is validated in the background (review §4, step 3).</summary>
    public const int BackgroundRows = 200;

    public const long MaxBytes = 10L * 1024 * 1024;

    public const string LegacyRef = "legacy_ref";
    public const string NameAr = "name_ar";
    public const string NameEn = "name_en";
    public const string Entity = "owner_entity";
    public const string Department = "department";
    public const string Sector = "sector";
    public const string Portfolio = "portfolio";
    public const string Initiative = "initiative";
    public const string FollowUpOwner = "portfolio_owner";
    public const string ProjectManager = "project_manager";
    public const string Status = "platform_status";
    public const string Governance = "governance_level";
    public const string Priority = "priority";
    public const string Strategic = "is_strategic";
    public const string Budget = "budget_sar";
    public const string Funding = "strategy_funded";
    public const string Start = "start_date";
    public const string End = "end_date";
    public const string Region = "region";
    public const string City = "city";
    public const string Description = "description";
    public const string PlannedProgress = "planned_progress";
    public const string ActualProgress = "actual_progress";
    public const string PlannedSpend = "planned_spend";
    public const string ActualSpend = "actual_spend";
    public const string Obstacle = "current_obstacle";
    public const string NextAction = "next_action";
    public const string ActionOwner = "action_owner";
    public const string ActionDue = "action_due";
    public const string EntityCommits = "entity_commits";
    public const string Notes = "notes";

    /// <summary>RPMO's own columns the platform does not store: each is skipped with an info note, and kept in the workbook attached to the batch.</summary>
    public static IReadOnlySet<string> Ignored { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "entity_type", "legacy_status", "project_type", "assumed_fields", "validation_warnings",
    };

    /// <summary>Each column the platform reads: its key, Arabic header, whether a row must fill it, and its cell shape.</summary>
    public static IReadOnlyList<(ImportColumn Column, string HeaderAr)> Columns { get; } =
    [
        (Text(LegacyRef, true, 100), "رقم المشروع (المرجع السابق)"),
        (Text(NameAr, true, 2000), "اسم المشروع (عربي)"),
        (Text(NameEn, false, 2000), "اسم المشروع (إنجليزي)"),
        (Text(Entity, true), "الجهة المالكة/المنفذة"),
        (Text(Department, true), "الإدارة"),
        (Text(Sector, true), "القطاع"),
        (Text(Portfolio, true), "المحفظة الاستراتيجية"),
        (Text(Initiative, false), "المبادرة الاستراتيجية"),
        (Text(FollowUpOwner, false), "مسؤول المحفظة"),
        (Text(ProjectManager, true), "مدير المشروع"),
        (Text(Status, false), "الحالة في المنصة"),
        (Text(Governance, true), "مستوى الحوكمة"),
        (Text(Strategic, false), "مشروع استراتيجي؟"),
        (Text(Priority, false), "الأولوية"),
        (new ImportColumn(Budget, ImportValueKind.Money, true, Budget, Budget), "قيمة المشروع (ريال)"),
        (Text(Funding, false), "ممول من ميزانية الاستراتيجية؟"),
        (new ImportColumn(Start, ImportValueKind.Date, true, Start, Start), "تاريخ البداية"),
        (new ImportColumn(End, ImportValueKind.Date, true, End, End), "تاريخ النهاية المستهدف"),
        (Text(Region, true), "المنطقة"),
        (Text(City, true), "المدينة/المركز"),
        (Text(Description, true, 2000), "وصف مختصر"),
        (Number(PlannedProgress), "نسبة الإنجاز المخططة"),
        (Number(ActualProgress), "نسبة الإنجاز الفعلية"),
        (Number(PlannedSpend), "نسبة الصرف المخططة"),
        (Number(ActualSpend), "نسبة الصرف الفعلية"),
        (Text(Obstacle, false, 2000), "المعوق الحالي"),
        (Text(NextAction, false, 2000), "الإجراء التالي"),
        (Text(ActionOwner, false), "مسؤول الإجراء"),
        (new ImportColumn(ActionDue, ImportValueKind.Date, false, ActionDue, ActionDue), "الموعد المستهدف للإجراء"),
        (Text(EntityCommits, false), "هل الجهة ملتزمة بالتحديث الدوري"),
        (Text(Notes, false, 2000), "ملاحظات"),
    ];

    public static ImportColumn ColumnOf(string key) => Columns.First(c => c.Column.Key == key).Column;

    private static ImportColumn Text(string key, bool required, int maxLength = 200) =>
        new(key, ImportValueKind.Text, required, key, key) { MaxLength = maxLength };

    private static ImportColumn Number(string key) => new(key, ImportValueKind.Number, false, key, key);
}
