using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>A project's tasks from a workbook, each row through <see cref="IProjectTaskService.CreateTaskAsync"/> as the form's. A parent is an earlier import's legacy ID.</summary>
internal sealed class ProjectTaskImportRegister(IProjectTaskService tasks, IScheduleActivityReader activities, ProjectImportScope scope) : IImportRegister
{
    private const string ParentColumn = "parent_task_legacy_id";
    private const string ActivityColumn = "schedule_activity_wbs";
    private const string TitleColumn = "title";
    private const string DescriptionColumn = "description";
    private const string AssigneeColumn = "assignee_username";
    private const string PriorityColumn = "priority_code";
    private const string StartColumn = "planned_start_date";
    private const string FinishColumn = "planned_finish_date";

    private static readonly Dictionary<string, string> ColumnOfField = new(StringComparer.Ordinal)
    {
        ["parentTaskId"] = ParentColumn,
        ["scheduleActivityId"] = ActivityColumn,
        ["assigneeUserId"] = AssigneeColumn,
        ["priorityItemId"] = PriorityColumn,
        ["title.text"] = TitleColumn,
        ["description.text"] = DescriptionColumn,
    };

    public string Code => ImportRegisterCodes.ProjectTask;

    public string OwningModule => "ProjectTask";

    public string Permission => PermissionCatalogue.TaskManage;

    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        ProjectImportScope.Column,
        new(TitleColumn, ImportValueKind.Text, Required: true, "The task's title.", "عنوان المهمة.") { MaxLength = 2000 },
        new(DescriptionColumn, ImportValueKind.Text, Required: false, "The task's description.", "وصف المهمة.") { MaxLength = 2000 },
        ProjectImportScope.Language,
        new(StartColumn, ImportValueKind.Date, Required: true, "The planned start date.", "تاريخ البداية المخطط."),
        new(FinishColumn, ImportValueKind.Date, Required: true, "The planned finish date, not before the start.", "تاريخ النهاية المخطط، لا يسبق البداية."),
        new(AssigneeColumn, ImportValueKind.Text, Required: false, "The assignee's username.", "اسم مستخدم المكلّف.") { MaxLength = 100 },
        new(PriorityColumn, ImportValueKind.Code, Required: false, "The priority's code.", "رمز الأولوية."),
        new(ActivityColumn, ImportValueKind.Text, Required: false, "The WBS code of the activity it executes.", "رمز النشاط الذي تنفذه.") { MaxLength = 50 },
        new(ParentColumn, ImportValueKind.Text, Required: false,
            "The legacy ID of its parent task, in the same project: imported earlier or in an earlier row.", "المعرّف السابق للمهمة الأم في المشروع نفسه: مستوردة سابقًا أو في صف سابق.") { MaxLength = ImportColumn.LegacyIdLength },
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
            [PriorityColumn] = await scope.CatalogueValuesAsync(MasterDataCatalogueCodes.Priority, cancellationToken).ConfigureAwait(false),
        };

    public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);
        Guid projectId = record.ProjectId!.Value;

        List<ImportCellIssue> issues = [];
        Guid? priority = await scope.ItemAsync(MasterDataCatalogueCodes.Priority, record, PriorityColumn, issues, cancellationToken).ConfigureAwait(false);
        Guid? assignee = await scope.UserAsync(record, AssigneeColumn, issues, cancellationToken).ConfigureAwait(false);
        Guid? activity = null;
        if (record.Text(ActivityColumn) is { } wbs
            && (activity = await activities.FindIdByWbsCodeAsync(projectId, wbs, cancellationToken).ConfigureAwait(false)) is null)
        {
            issues.Add(new ImportCellIssue(ActivityColumn, ImportIssueCodes.ReferenceNotFound));
        }

        Guid? parent = null;
        if (record.Text(ParentColumn) is { } parentLegacyId
            && (parent = await context.FindImportedAsync(Code, ImportLineage.Key(projectId, parentLegacyId), cancellationToken).ConfigureAwait(false)) is null)
        {
            issues.Add(new ImportCellIssue(ParentColumn, ImportIssueCodes.ReferenceNotFound));
        }

        if (record.Date(FinishColumn) < record.Date(StartColumn))
        {
            issues.Add(new ImportCellIssue(FinishColumn, ValidationCode.DateBeforeStart));
        }

        if (issues.Count > 0)
        {
            return ImportRowResult.Rejected(issues);
        }

        ProjectTaskDraft draft = new(
            projectId,
            parent,
            activity,
            ProjectImportScope.Narrative(record, TitleColumn)!,
            ProjectImportScope.Narrative(record, DescriptionColumn),
            assignee,
            priority,
            record.Date(StartColumn)!.Value,
            record.Date(FinishColumn)!.Value);
        AdministrationResult<Versioned<ProjectTaskDetail>> created = await tasks.CreateTaskAsync(context.ActorId, draft, cancellationToken).ConfigureAwait(false);
        return created.Succeeded ? ImportRowResult.Created(created.Value.Value.Id) : AdministrationImportIssues.Rejected(created.Error, ColumnOfField);
    }
}
