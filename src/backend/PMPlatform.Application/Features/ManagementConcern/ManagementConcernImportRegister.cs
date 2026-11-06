using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Domain.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>A project's issues and challenges from a workbook, each raised through <see cref="IManagementConcernService.RaiseAsync"/> as the form's, with no impacts: those are assessed on screen.</summary>
internal sealed class ManagementConcernImportRegister(IManagementConcernService concerns, ProjectImportScope scope) : IImportRegister
{
    private const string TypeColumn = "concern_type";
    private const string TitleColumn = "title";
    private const string DescriptionColumn = "description";
    private const string CategoryColumn = "category_code";
    private const string PriorityColumn = "priority_code";
    private const string TargetColumn = "target_resolution_date";

    private static readonly Dictionary<string, string> ColumnOfField = new(StringComparer.Ordinal)
    {
        ["categoryItemId"] = CategoryColumn,
        ["priorityItemId"] = PriorityColumn,
        ["title.text"] = TitleColumn,
        ["description.text"] = DescriptionColumn,
    };

    public string Code => ImportRegisterCodes.ManagementConcern;

    public string OwningModule => "ManagementConcern";

    public string Permission => PermissionCatalogue.ConcernRaise;

    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        ProjectImportScope.Column,
        ImportColumn.ChoiceOf<ConcernType>(TypeColumn, required: true, "Whether the row is an ISSUE or a CHALLENGE.", "هل الصف قضية ISSUE أم تحدٍّ CHALLENGE."),
        new(TitleColumn, ImportValueKind.Text, Required: true, "The title.", "العنوان.") { MaxLength = 2000 },
        new(DescriptionColumn, ImportValueKind.Text, Required: true, "The description.", "الوصف.") { MaxLength = 2000 },
        ProjectImportScope.Language,
        new(CategoryColumn, ImportValueKind.Code, Required: true, "The category's code.", "رمز الفئة."),
        new(PriorityColumn, ImportValueKind.Code, Required: true, "The priority's code.", "رمز الأولوية."),
        new(TargetColumn, ImportValueKind.Date, Required: false, "The target resolution date.", "تاريخ الحل المستهدف."),
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
            [CategoryColumn] = await scope.CatalogueValuesAsync(MasterDataCatalogueCodes.ConcernCategory, cancellationToken).ConfigureAwait(false),
            [PriorityColumn] = await scope.CatalogueValuesAsync(MasterDataCatalogueCodes.Priority, cancellationToken).ConfigureAwait(false),
        };

    public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);

        List<ImportCellIssue> issues = [];
        Guid? category = await scope.ItemAsync(MasterDataCatalogueCodes.ConcernCategory, record, CategoryColumn, issues, cancellationToken).ConfigureAwait(false);
        Guid? priority = await scope.ItemAsync(MasterDataCatalogueCodes.Priority, record, PriorityColumn, issues, cancellationToken).ConfigureAwait(false);
        if (issues.Count > 0)
        {
            return ImportRowResult.Rejected(issues);
        }

        ConcernDraft draft = new(
            record.ProjectId!.Value,
            record.Choice<ConcernType>(TypeColumn)!.Value,
            ProjectImportScope.Narrative(record, TitleColumn)!,
            ProjectImportScope.Narrative(record, DescriptionColumn)!,
            category!.Value,
            priority!.Value,
            [],
            record.Date(TargetColumn));
        AdministrationResult<Versioned<ConcernDetail>> raised = await concerns.RaiseAsync(context.ActorId, draft, cancellationToken).ConfigureAwait(false);
        return raised.Succeeded ? ImportRowResult.Created(raised.Value.Value.Id) : AdministrationImportIssues.Rejected(raised.Error, ColumnOfField);
    }
}
