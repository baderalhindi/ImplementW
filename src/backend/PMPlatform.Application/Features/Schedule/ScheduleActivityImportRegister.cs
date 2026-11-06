using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>A project's activities from a workbook, each row through <see cref="IScheduleService.CreateActivityAsync"/> as the form's (data-import.md D-12).</summary>
internal sealed class ScheduleActivityImportRegister(IScheduleService schedules, IScheduleActivityReader activities, ProjectImportScope scope) : IImportRegister
{
    private const string ParentColumn = "parent_wbs_code";
    private const string WbsColumn = "wbs_code";
    private const string NameColumn = "name";
    private const string StartColumn = "requested_start_date";
    private const string DurationColumn = "planned_duration_days";
    private const string SortColumn = "sort_order";
    private const int MaxDurationDays = 9999;

    private static readonly Dictionary<string, string> ColumnOfField = new(StringComparer.Ordinal)
    {
        ["parentActivityId"] = ParentColumn,
        ["name.text"] = NameColumn,
    };

    public string Code => ImportRegisterCodes.ScheduleActivity;

    public string OwningModule => "Schedule";

    public string Permission => PermissionCatalogue.ScheduleEdit;

    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        ProjectImportScope.Column,
        new(WbsColumn, ImportValueKind.Text, Required: true, "The activity's WBS code, unique in the project.", "رمز هيكل تجزئة العمل للنشاط، فريد في المشروع.") { MaxLength = 50 },
        new(ParentColumn, ImportValueKind.Text, Required: false,
            "The WBS code of the activity it sits under: an existing activity or an earlier row.", "رمز النشاط الأعلى: نشاط قائم أو صف سابق.") { MaxLength = 50 },
        new(NameColumn, ImportValueKind.Text, Required: true, "The activity's name.", "اسم النشاط.") { MaxLength = 2000 },
        ProjectImportScope.Language,
        new(StartColumn, ImportValueKind.Date, Required: true, "The requested start date.", "تاريخ البداية المطلوب."),
        new(DurationColumn, ImportValueKind.WholeNumber, Required: true, "The planned duration, in working days: 1 to 9999.", "المدة المخططة بأيام العمل: من 1 إلى 9999."),
        new(SortColumn, ImportValueKind.WholeNumber, Required: false, "Its position among its siblings; empty is 0.", "ترتيبه بين الأنشطة الشقيقة؛ الفارغ يعني 0."),
    ];

    public Task<ImportRowScope> ScopeAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return scope.PlaceAsync(record, context.ActorId, Permission, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>>> ReferenceValuesAsync(Guid actorId, CancellationToken cancellationToken) =>
        new Dictionary<string, IReadOnlyList<ImportReferenceValue>>
        {
            [ProjectImportScope.ProjectColumn] = await scope.ReachableAsync(actorId, Permission, cancellationToken).ConfigureAwait(false),
        };

    public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);
        Guid projectId = record.ProjectId!.Value;

        Guid? parent = null;
        if (record.Text(ParentColumn) is { } parentCode
            && (parent = await activities.FindIdByWbsCodeAsync(projectId, parentCode, cancellationToken).ConfigureAwait(false)) is null)
        {
            return ImportRowResult.Rejected(ParentColumn, ImportIssueCodes.ReferenceNotFound);
        }

        if (record.WholeNumber(DurationColumn) is < 1 or > MaxDurationDays)
        {
            return ImportRowResult.Rejected(DurationColumn, ValidationCode.OutOfRange);
        }

        if (record.WholeNumber(SortColumn) is < 0)
        {
            return ImportRowResult.Rejected(SortColumn, ValidationCode.OutOfRange);
        }

        ScheduleActivityDraft draft = new(
            projectId,
            parent,
            record.Text(WbsColumn)!,
            ProjectImportScope.Narrative(record, NameColumn)!,
            record.Date(StartColumn)!.Value,
            record.WholeNumber(DurationColumn)!.Value,
            record.WholeNumber(SortColumn) ?? 0);
        AdministrationResult<Versioned<ScheduleActivityDetail>> created = await schedules.CreateActivityAsync(context.ActorId, draft, cancellationToken).ConfigureAwait(false);
        return created.Succeeded ? ImportRowResult.Created(created.Value.Value.Id) : AdministrationImportIssues.Rejected(created.Error, ColumnOfField);
    }
}
