using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.Risk.Contracts;

namespace PMPlatform.Application.Features.Risk;

/// <summary>A project's risks from a workbook, each IDENTIFIED through <see cref="IRiskService.RegisterRiskAsync"/> as the form's. A rating comes from an assessment, never from a cell.</summary>
internal sealed class RiskImportRegister(IRiskService risks, ProjectImportScope scope) : IImportRegister
{
    private const string TitleColumn = "title";
    private const string DescriptionColumn = "description";
    private const string CategoryColumn = "risk_category_code";
    private const string OwnerColumn = "owner_username";
    private const string IdentifiedColumn = "identified_date";
    private const string ReviewColumn = "next_review_date";

    private static readonly Dictionary<string, string> ColumnOfField = new(StringComparer.Ordinal)
    {
        ["riskCategoryItemId"] = CategoryColumn,
        ["ownerUserId"] = OwnerColumn,
        ["title.text"] = TitleColumn,
        ["description.text"] = DescriptionColumn,
    };

    public string Code => ImportRegisterCodes.Risk;

    public string OwningModule => "Risk";

    public string Permission => PermissionCatalogue.RiskManage;

    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        ProjectImportScope.Column,
        new(TitleColumn, ImportValueKind.Text, Required: true, "The risk's title.", "عنوان الخطر.") { MaxLength = 2000 },
        new(DescriptionColumn, ImportValueKind.Text, Required: true, "The risk's description.", "وصف الخطر.") { MaxLength = 2000 },
        ProjectImportScope.Language,
        new(CategoryColumn, ImportValueKind.Code, Required: true, "The risk category's code.", "رمز فئة الخطر."),
        new(OwnerColumn, ImportValueKind.Text, Required: false, "The risk owner's username.", "اسم مستخدم مالك الخطر.") { MaxLength = 100 },
        new(IdentifiedColumn, ImportValueKind.Date, Required: true, "The date it was identified.", "تاريخ تحديده."),
        new(ReviewColumn, ImportValueKind.Date, Required: false, "The next review date, not before it was identified.", "تاريخ المراجعة القادمة، لا يسبق تاريخ التحديد."),
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
            [CategoryColumn] = await scope.CatalogueValuesAsync(MasterDataCatalogueCodes.RiskCategory, cancellationToken).ConfigureAwait(false),
        };

    public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);

        List<ImportCellIssue> issues = [];
        Guid? category = await scope.ItemAsync(MasterDataCatalogueCodes.RiskCategory, record, CategoryColumn, issues, cancellationToken).ConfigureAwait(false);
        Guid? owner = await scope.UserAsync(record, OwnerColumn, issues, cancellationToken).ConfigureAwait(false);
        if (record.Date(ReviewColumn) < record.Date(IdentifiedColumn))
        {
            issues.Add(new ImportCellIssue(ReviewColumn, ValidationCode.DateBeforeStart));
        }

        if (issues.Count > 0)
        {
            return ImportRowResult.Rejected(issues);
        }

        RiskDraft draft = new(
            record.ProjectId!.Value,
            ProjectImportScope.Narrative(record, TitleColumn)!,
            ProjectImportScope.Narrative(record, DescriptionColumn)!,
            category!.Value,
            owner,
            record.Date(IdentifiedColumn)!.Value,
            record.Date(ReviewColumn));
        AdministrationResult<Versioned<RiskDetail>> created = await risks.RegisterRiskAsync(context.ActorId, draft, cancellationToken).ConfigureAwait(false);
        return created.Succeeded ? ImportRowResult.Created(created.Value.Value.Id) : AdministrationImportIssues.Rejected(created.Error, ColumnOfField);
    }
}
