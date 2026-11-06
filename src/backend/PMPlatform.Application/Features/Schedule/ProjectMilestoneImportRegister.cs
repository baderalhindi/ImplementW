using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>A project's milestones from a workbook, each PLANNED through <see cref="IProjectMilestoneService.CreateAsync"/> as the form's. An achievement is still claimed on screen, with its evidence.</summary>
internal sealed class ProjectMilestoneImportRegister(IProjectMilestoneService milestones, IScheduleActivityReader activities, ProjectImportScope scope) : IImportRegister
{
    private const string ActivityColumn = "schedule_activity_wbs";
    private const string TitleColumn = "title";
    private const string CategoryColumn = "milestone_category_code";
    private const string ForecastColumn = "forecast_date";
    private const string SortColumn = "sort_order";

    private static readonly Dictionary<string, string> ColumnOfField = new(StringComparer.Ordinal)
    {
        ["scheduleActivityId"] = ActivityColumn,
        ["milestoneCategoryItemId"] = CategoryColumn,
        ["title.text"] = TitleColumn,
    };

    public string Code => ImportRegisterCodes.ProjectMilestone;

    public string OwningModule => "Schedule";

    public string Permission => PermissionCatalogue.ScheduleEdit;

    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        ProjectImportScope.Column,
        new(TitleColumn, ImportValueKind.Text, Required: true, "The milestone's title.", "عنوان المعلم.") { MaxLength = 2000 },
        ProjectImportScope.Language,
        new(CategoryColumn, ImportValueKind.Code, Required: true, "The milestone category's code.", "رمز فئة المعلم."),
        new(ForecastColumn, ImportValueKind.Date, Required: true, "The forecast date.", "التاريخ المتوقع."),
        new(ActivityColumn, ImportValueKind.Text, Required: false, "The WBS code of the activity it belongs to.", "رمز النشاط الذي يتبع له.") { MaxLength = 50 },
        new(SortColumn, ImportValueKind.WholeNumber, Required: false, "Its position in the list; empty is 0.", "ترتيبه في القائمة؛ الفارغ يعني 0."),
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
            [CategoryColumn] = await scope.CatalogueValuesAsync(MasterDataCatalogueCodes.MilestoneCategory, cancellationToken).ConfigureAwait(false),
        };

    public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);
        Guid projectId = record.ProjectId!.Value;

        List<ImportCellIssue> issues = [];
        Guid? category = await scope.ItemAsync(MasterDataCatalogueCodes.MilestoneCategory, record, CategoryColumn, issues, cancellationToken).ConfigureAwait(false);
        Guid? activity = null;
        if (record.Text(ActivityColumn) is { } wbs
            && (activity = await activities.FindIdByWbsCodeAsync(projectId, wbs, cancellationToken).ConfigureAwait(false)) is null)
        {
            issues.Add(new ImportCellIssue(ActivityColumn, ImportIssueCodes.ReferenceNotFound));
        }

        if (record.WholeNumber(SortColumn) is < 0)
        {
            issues.Add(new ImportCellIssue(SortColumn, ValidationCode.OutOfRange));
        }

        if (issues.Count > 0)
        {
            return ImportRowResult.Rejected(issues);
        }

        ProjectMilestoneDraft draft = new(
            projectId,
            activity,
            ProjectImportScope.Narrative(record, TitleColumn)!,
            category!.Value,
            record.Date(ForecastColumn)!.Value,
            record.WholeNumber(SortColumn) ?? 0);
        AdministrationResult<Versioned<ProjectMilestoneDetail>> created = await milestones.CreateAsync(context.ActorId, draft, cancellationToken).ConfigureAwait(false);
        return created.Succeeded ? ImportRowResult.Created(created.Value.Value.Id) : AdministrationImportIssues.Rejected(created.Error, ColumnOfField);
    }
}
