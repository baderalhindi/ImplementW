using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// ADM-013's migration workbook (TASK-102): each row through <see cref="IExternalEntityAdministrationService"/>. The type is a published
/// EXTERNAL_ENTITY_TYPE code and the sponsor an active internal user by username; an entity the legacy system holds SUSPENDED or RETIRED
/// is created ACTIVE (ADR-013) and then moved there, each step audited.
/// </summary>
internal sealed class ExternalEntityImportRegister(
    IExternalEntityAdministrationService entities, IOrganizationDirectory directory, IUserLookup users, CatalogueCodeBook catalogues) : IImportRegister
{
    private const string CodeColumn = "code";
    private const string NameArColumn = "name_ar";
    private const string NameEnColumn = "name_en";
    private const string EntityTypeColumn = "entity_type_code";
    private const string SponsorColumn = "sponsor_username";
    private const string StatusColumn = "status";

    private static readonly Dictionary<string, string> ColumnOfField = new(StringComparer.Ordinal)
    {
        ["entityTypeItemId"] = EntityTypeColumn,
        ["sponsorUserId"] = SponsorColumn,
    };

    public string Code => ImportRegisterCodes.ExternalEntity;

    public string OwningModule => "IdentityAccess";

    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        new(CodeColumn, ImportValueKind.Code, Required: true, "The entity's code, unique.", "رمز الجهة، فريد."),
        new(NameArColumn, ImportValueKind.Text, Required: true, "The name in Arabic.", "الاسم بالعربية."),
        new(NameEnColumn, ImportValueKind.Text, Required: true, "The name in English.", "الاسم بالإنجليزية."),
        new(EntityTypeColumn, ImportValueKind.Code, Required: true, "The entity type's code.", "رمز نوع الجهة."),
        new(SponsorColumn, ImportValueKind.Text, Required: false, "The username of the AHDA sponsor: an active internal user.", "اسم مستخدم الراعي في الهيئة: مستخدم داخلي نشط.") { MaxLength = 100 },
        ImportColumn.ChoiceOf(StatusColumn, required: false, "The entity's status in the legacy system. Empty means ACTIVE.", "حالة الجهة في النظام السابق. الفارغ يعني ACTIVE.",
            ExternalEntityStatus.Active, ExternalEntityStatus.Suspended, ExternalEntityStatus.Retired),
    ];

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>>> ReferenceValuesAsync(CancellationToken cancellationToken) =>
        new Dictionary<string, IReadOnlyList<ImportReferenceValue>>
        {
            [EntityTypeColumn] = await catalogues.ReferenceValuesAsync(MasterDataCatalogueCodes.ExternalEntityType, cancellationToken).ConfigureAwait(false),
        };

    public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);

        if (await directory.FindExternalEntityIdAsync(record.Text(CodeColumn)!, cancellationToken).ConfigureAwait(false) is not null)
        {
            return ImportRowResult.Rejected(CodeColumn, ImportIssueCodes.AlreadyExists);
        }

        if (await catalogues.FindAsync(MasterDataCatalogueCodes.ExternalEntityType, record.Text(EntityTypeColumn)!, cancellationToken).ConfigureAwait(false) is not { } entityType)
        {
            return ImportRowResult.Rejected(EntityTypeColumn, ImportIssueCodes.ReferenceNotFound);
        }

        Guid? sponsorId = null;
        if (record.Text(SponsorColumn) is { } sponsor && (sponsorId = await users.FindIdByUsernameAsync(sponsor, cancellationToken).ConfigureAwait(false)) is null)
        {
            return ImportRowResult.Rejected(SponsorColumn, ImportIssueCodes.ReferenceNotFound);
        }

        ExternalEntityDraft draft = new(record.Text(CodeColumn)!, new BilingualLabel(record.Text(NameArColumn)!, record.Text(NameEnColumn)!), entityType.Id, sponsorId);
        AdministrationResult<Versioned<ExternalEntityDetail>> created = await entities.CreateAsync(context.ActorId, draft, cancellationToken).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            return AdministrationImportIssues.Rejected(created.Error, ColumnOfField);
        }

        Guid entityId = created.Value.Value.Id;
        AdministrationResult<Versioned<ExternalEntityDetail>>? moved = record.Choice<ExternalEntityStatus>(StatusColumn) switch
        {
            ExternalEntityStatus.Suspended => await entities.SuspendAsync(context.ActorId, entityId, expectedVersion: null, cancellationToken).ConfigureAwait(false),
            ExternalEntityStatus.Retired => await entities.RetireAsync(context.ActorId, entityId, expectedVersion: null, cancellationToken).ConfigureAwait(false),
            ExternalEntityStatus.Active or null => null,
            _ => throw new InvalidOperationException("Unknown external entity status."),
        };
        return moved?.Error is { } error ? AdministrationImportIssues.Rejected(error, ColumnOfField) : ImportRowResult.Created(entityId);
    }
}
