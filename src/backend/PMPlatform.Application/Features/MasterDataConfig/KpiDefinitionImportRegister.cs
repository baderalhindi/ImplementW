using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>
/// The KPI catalogue's migration workbook (TASK-102), each row through <see cref="IKpiDefinitionAdministrationService"/>. A definition is
/// created DRAFT, so two other administrators validate and publish it (ERD D-12; data-import.md D-6). The unit is a published KPI_UNIT code.
/// </summary>
internal sealed class KpiDefinitionImportRegister(
    IKpiDefinitionAdministrationService definitions, IMasterDataRepository masterData, CatalogueCodeBook catalogues) : IImportRegister
{
    private const string CodeColumn = "code";
    private const string NameArColumn = "name_ar";
    private const string NameEnColumn = "name_en";
    private const string DescriptionArColumn = "description_ar";
    private const string DescriptionEnColumn = "description_en";
    private const string UnitColumn = "unit_code";
    private const string DirectionColumn = "direction";

    private static readonly Dictionary<string, string> ColumnOfField = new(StringComparer.Ordinal) { ["unitItemId"] = UnitColumn };

    public string Code => "KPI_DEFINITION";

    public string OwningModule => "MasterDataConfig";

    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        new(CodeColumn, ImportValueKind.Code, Required: true, "The KPI's code, unique.", "رمز المؤشر، فريد."),
        new(NameArColumn, ImportValueKind.Text, Required: true, "The name in Arabic.", "الاسم بالعربية."),
        new(NameEnColumn, ImportValueKind.Text, Required: true, "The name in English.", "الاسم بالإنجليزية."),
        new(DescriptionArColumn, ImportValueKind.Text, Required: false, "The description in Arabic; give both languages or neither.", "الوصف بالعربية؛ أدخل اللغتين معًا أو اتركهما."),
        new(DescriptionEnColumn, ImportValueKind.Text, Required: false, "The description in English.", "الوصف بالإنجليزية."),
        new(UnitColumn, ImportValueKind.Code, Required: true, "The unit's code.", "رمز وحدة القياس."),
        ImportColumn.ChoiceOf<KpiDirection>(DirectionColumn, required: true, "Which way is better.", "أي اتجاه أفضل."),
    ];

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>>> ReferenceValuesAsync(CancellationToken cancellationToken) =>
        new Dictionary<string, IReadOnlyList<ImportReferenceValue>>
        {
            [UnitColumn] = await catalogues.ReferenceValuesAsync(MasterDataCatalogueCodes.KpiUnit, cancellationToken).ConfigureAwait(false),
        };

    public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);

        if (await masterData.FindKpiDefinitionIdAsync(record.Text(CodeColumn)!, cancellationToken).ConfigureAwait(false) is not null)
        {
            return ImportRowResult.Rejected(CodeColumn, ImportIssueCodes.AlreadyExists);
        }

        if (await catalogues.FindAsync(MasterDataCatalogueCodes.KpiUnit, record.Text(UnitColumn)!, cancellationToken).ConfigureAwait(false) is not { } unit)
        {
            return ImportRowResult.Rejected(UnitColumn, ImportIssueCodes.ReferenceNotFound);
        }

        if ((record.Text(DescriptionArColumn) is null) != (record.Text(DescriptionEnColumn) is null))
        {
            return ImportRowResult.Rejected(record.Text(DescriptionArColumn) is null ? DescriptionArColumn : DescriptionEnColumn, ValidationCode.Required);
        }

        BilingualLabel? description = record.Text(DescriptionArColumn) is { } ar ? new BilingualLabel(ar, record.Text(DescriptionEnColumn)!) : null;
        KpiDefinitionDraft draft = new(
            record.Text(CodeColumn)!, new BilingualLabel(record.Text(NameArColumn)!, record.Text(NameEnColumn)!), description, unit.Id,
            record.Choice<KpiDirection>(DirectionColumn)!.Value);
        AdministrationResult<Versioned<KpiDefinitionDetail>> created = await definitions.CreateAsync(context.ActorId, draft, cancellationToken).ConfigureAwait(false);
        return created.Succeeded ? ImportRowResult.Created(created.Value.Value.Id) : AdministrationImportIssues.Rejected(created.Error, ColumnOfField);
    }
}
