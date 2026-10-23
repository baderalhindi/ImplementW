using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// ADM-004's migration workbook (TASK-102): each row through <see cref="IUserAdministrationService"/>, with ADM-004's rules — an internal
/// user is linked to the directory by subject (ADR-007), an external user belongs to an entity (ADR-013). A user the legacy system holds
/// DISABLED is created and then disabled. Role assignments are not part of a user row: they are bound to a profile version (ADR-018).
/// </summary>
internal sealed class UserImportRegister(IUserAdministrationService users, IUserLookup lookup, IOrganizationDirectory directory) : IImportRegister
{
    private const string UserTypeColumn = "user_type";
    private const string UsernameColumn = "username";
    private const string DisplayNameColumn = "display_name";
    private const string EmailColumn = "email";
    private const string MobileNumberColumn = "mobile_number";
    private const string LanguageColumn = "preferred_language";
    private const string DirectorySubjectColumn = "directory_subject_id";
    private const string JobTitleColumn = "job_title";
    private const string EntityColumn = "external_entity_code";
    private const string StatusColumn = "status";

    private static readonly Dictionary<string, string> ColumnOfField = new(StringComparer.Ordinal) { ["externalEntityId"] = EntityColumn };

    public string Code => "USER";

    public string OwningModule => "IdentityAccess";

    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        ImportColumn.ChoiceOf(UserTypeColumn, required: true, "INTERNAL for an AHDA employee in the directory, EXTERNAL for an entity's user.",
            "INTERNAL لموظف الهيئة في الدليل، وEXTERNAL لمستخدم جهة خارجية.", UserType.Internal, UserType.External),
        new(UsernameColumn, ImportValueKind.Text, Required: true, "The sign-in name, unique.", "اسم الدخول، فريد.") { MaxLength = 100 },
        new(DisplayNameColumn, ImportValueKind.Text, Required: true, "The name shown on the platform.", "الاسم الظاهر في المنصة."),
        new(EmailColumn, ImportValueKind.Email, Required: true, "The e-mail address.", "البريد الإلكتروني."),
        new(MobileNumberColumn, ImportValueKind.PhoneNumber, Required: false,
            "The mobile number in E.164 (+9665…). SMS is sent only once its holder has verified it.", "رقم الجوال بصيغة E.164 (+9665…). لا تُرسل رسائل إلا بعد أن يتحقق منه صاحبه."),
        new(LanguageColumn, ImportValueKind.Choice, Required: false, "ar or en. Empty means ar.", "ar أو en. الفارغ يعني ar.") { Choices = ["ar", "en"] },
        new(DirectorySubjectColumn, ImportValueKind.Text, Required: false,
            "Required for an INTERNAL user: the directory (AD) subject sign-in matches.", "إلزامي للمستخدم الداخلي: معرّف الدليل (AD) الذي يُطابَق عند الدخول."),
        new(JobTitleColumn, ImportValueKind.Text, Required: false,
            "EXTERNAL users only; an internal user's title comes from the directory.", "للمستخدم الخارجي فقط؛ مسمى الموظف الداخلي يأتي من الدليل."),
        new(EntityColumn, ImportValueKind.Code, Required: false, "Required for an EXTERNAL user: the code of the entity they belong to.", "إلزامي للمستخدم الخارجي: رمز الجهة التي ينتمي إليها."),
        ImportColumn.ChoiceOf(StatusColumn, required: false, "The user's status in the legacy system. Empty means ACTIVE.", "حالة المستخدم في النظام السابق. الفارغ يعني ACTIVE.",
            UserStatus.Active, UserStatus.Disabled),
    ];

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>>> ReferenceValuesAsync(CancellationToken cancellationToken) =>
        new Dictionary<string, IReadOnlyList<ImportReferenceValue>>
        {
            [EntityColumn] = [.. (await directory.ListActiveExternalEntityCodesAsync(cancellationToken).ConfigureAwait(false)).Select(e => new ImportReferenceValue(e.Code, e.Name.Ar, e.Name.En))],
        };

    public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);

        UserType userType = record.Choice<UserType>(UserTypeColumn)!.Value;
        if (userType == UserType.Internal && record.Text(DirectorySubjectColumn) is null)
        {
            return ImportRowResult.Rejected(DirectorySubjectColumn, ValidationCode.Required);
        }

        if (userType == UserType.Internal && record.Text(EntityColumn) is not null)
        {
            return ImportRowResult.Rejected(EntityColumn, ValidationCode.NotAllowed);
        }

        Guid? entityId = null;
        if (userType == UserType.External
            && (record.Text(EntityColumn) is not { } entityCode
                || (entityId = await directory.FindExternalEntityIdAsync(entityCode, cancellationToken).ConfigureAwait(false)) is null))
        {
            return ImportRowResult.Rejected(EntityColumn, record.Text(EntityColumn) is null ? ValidationCode.Required : ImportIssueCodes.ReferenceNotFound);
        }

        if (await lookup.FindIdByUsernameAsync(record.Text(UsernameColumn)!, cancellationToken).ConfigureAwait(false) is not null)
        {
            return ImportRowResult.Rejected(UsernameColumn, ImportIssueCodes.AlreadyExists);
        }

        UserDraft draft = new(
            userType,
            record.Text(UsernameColumn)!,
            record.Text(DisplayNameColumn)!,
            record.Text(EmailColumn)!,
            record.Text(MobileNumberColumn),
            record.Text(LanguageColumn) is { } language ? LanguageCode.Parse(language) : Language.Ar,
            record.Text(DirectorySubjectColumn),
            record.Text(JobTitleColumn),
            entityId);
        AdministrationResult<Versioned<UserDetail>> created = await users.CreateAsync(context.ActorId, draft, cancellationToken).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            return AdministrationImportIssues.Rejected(created.Error, ColumnOfField);
        }

        Guid userId = created.Value.Value.Id;
        return record.Choice<UserStatus>(StatusColumn) == UserStatus.Disabled
               && (await users.DisableAsync(context.ActorId, userId, expectedVersion: null, cancellationToken).ConfigureAwait(false)).Error is { } disabling
            ? AdministrationImportIssues.Rejected(disabling, ColumnOfField)
            : ImportRowResult.Created(userId);
    }
}
