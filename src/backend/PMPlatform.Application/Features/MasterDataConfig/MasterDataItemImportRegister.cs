using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>
/// ADM-020–029's migration workbook (TASK-102): the legacy code lists, each row through <see cref="IMasterDataAdministrationService"/>.
/// An item is created DRAFT, authored by the importer, like one entered on screen: the governed lifecycle's separation of duties holds,
/// so two other administrators validate and publish it (ERD D-12) before any record may use it (data-import.md D-6).
/// </summary>
internal sealed class MasterDataItemImportRegister(IMasterDataAdministrationService masterDataAdministration, IMasterDataRepository masterData) : IImportRegister
{
    private const string CatalogueColumn = "catalogue_code";
    private const string CodeColumn = "code";
    private const string LabelArColumn = "label_ar";
    private const string LabelEnColumn = "label_en";
    private const string DescriptionArColumn = "description_ar";
    private const string DescriptionEnColumn = "description_en";
    private const string ParentColumn = "parent_code";
    private const string SortOrderColumn = "sort_order";

    private static readonly Dictionary<string, string> ColumnOfField = new(StringComparer.Ordinal)
    {
        ["catalogueId"] = CatalogueColumn,
        ["parentItemId"] = ParentColumn,
    };

    public string Code => "MASTER_DATA_ITEM";

    public string OwningModule => "MasterDataConfig";

    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        new(CatalogueColumn, ImportValueKind.Code, Required: true, "The catalogue the item belongs to.", "القائمة التي ينتمي إليها البند."),
        new(CodeColumn, ImportValueKind.Code, Required: true, "The item's code, unique in its catalogue.", "رمز البند، فريد في قائمته."),
        new(LabelArColumn, ImportValueKind.Text, Required: true, "The label in Arabic.", "التسمية بالعربية."),
        new(LabelEnColumn, ImportValueKind.Text, Required: true, "The label in English.", "التسمية بالإنجليزية."),
        new(DescriptionArColumn, ImportValueKind.Text, Required: false, "The description in Arabic; give both languages or neither.", "الوصف بالعربية؛ أدخل اللغتين معًا أو اتركهما."),
        new(DescriptionEnColumn, ImportValueKind.Text, Required: false, "The description in English.", "الوصف بالإنجليزية."),
        new(ParentColumn, ImportValueKind.Code, Required: false,
            "The parent item's code in the same catalogue, where the catalogue has a hierarchy: an existing item or a row above this one.",
            "رمز البند الأب في القائمة نفسها إن كانت هرمية: بند قائم أو صف يسبق هذا الصف."),
        new(SortOrderColumn, ImportValueKind.WholeNumber, Required: false, "The display order. Empty means 0.", "ترتيب العرض. الفارغ يعني 0."),
    ];

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>>> ReferenceValuesAsync(CancellationToken cancellationToken) =>
        new Dictionary<string, IReadOnlyList<ImportReferenceValue>>
        {
            [CatalogueColumn] = [.. (await masterData.ListCataloguesAsync(cancellationToken).ConfigureAwait(false)).Select(c => new ImportReferenceValue(c.Code, c.Name.Ar, c.Name.En))],
        };

    public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);

        if (await masterData.FindCatalogueByCodeAsync(record.Text(CatalogueColumn)!, cancellationToken).ConfigureAwait(false) is not { } catalogue)
        {
            return ImportRowResult.Rejected(CatalogueColumn, ImportIssueCodes.ReferenceNotFound);
        }

        if (await masterData.FindItemIdAsync(catalogue.Id, record.Text(CodeColumn)!, cancellationToken).ConfigureAwait(false) is not null)
        {
            return ImportRowResult.Rejected(CodeColumn, ImportIssueCodes.AlreadyExists);
        }

        Guid? parentId = null;
        if (record.Text(ParentColumn) is { } parentCode
            && (parentId = await masterData.FindItemIdAsync(catalogue.Id, parentCode, cancellationToken).ConfigureAwait(false)) is null)
        {
            return ImportRowResult.Rejected(ParentColumn, ImportIssueCodes.ReferenceNotFound);
        }

        if ((record.Text(DescriptionArColumn) is null) != (record.Text(DescriptionEnColumn) is null))
        {
            return ImportRowResult.Rejected(record.Text(DescriptionArColumn) is null ? DescriptionArColumn : DescriptionEnColumn, ValidationCode.Required);
        }

        BilingualLabel? description = record.Text(DescriptionArColumn) is { } ar ? new BilingualLabel(ar, record.Text(DescriptionEnColumn)!) : null;
        MasterDataItemDraft draft = new(
            catalogue.Id, record.Text(CodeColumn)!, new BilingualLabel(record.Text(LabelArColumn)!, record.Text(LabelEnColumn)!), description, parentId,
            record.WholeNumber(SortOrderColumn) ?? 0);
        AdministrationResult<Versioned<MasterDataItemDetail>> created = await masterDataAdministration.CreateItemAsync(context.ActorId, draft, cancellationToken).ConfigureAwait(false);
        return created.Succeeded ? ImportRowResult.Created(created.Value.Value.Id) : AdministrationImportIssues.Rejected(created.Error, ColumnOfField);
    }
}
