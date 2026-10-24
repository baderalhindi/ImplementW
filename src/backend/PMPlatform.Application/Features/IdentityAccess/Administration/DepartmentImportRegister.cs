using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// ADM-012's migration workbook (TASK-102): each row through <see cref="IDepartmentAdministrationService"/>, so it meets the form's rules
/// and is audited as a create. A parent is named by code, of an existing department or of a row above it; a department the legacy
/// system holds inactive is created and then deactivated.
/// </summary>
internal sealed class DepartmentImportRegister(IDepartmentAdministrationService departments, IOrganizationDirectory directory) : IImportRegister
{
    private const string CodeColumn = "code";
    private const string NameArColumn = "name_ar";
    private const string NameEnColumn = "name_en";
    private const string ParentCodeColumn = "parent_code";
    private const string DirectoryReferenceColumn = "directory_reference";
    private const string IsActiveColumn = "is_active";

    private static readonly Dictionary<string, string> ColumnOfField = new(StringComparer.Ordinal) { ["parentDepartmentId"] = ParentCodeColumn };

    public string Code => ImportRegisterCodes.Department;

    public string OwningModule => "IdentityAccess";

    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        new(CodeColumn, ImportValueKind.Code, Required: true, "The department's code, unique.", "رمز الإدارة، فريد."),
        new(NameArColumn, ImportValueKind.Text, Required: true, "The name in Arabic.", "الاسم بالعربية."),
        new(NameEnColumn, ImportValueKind.Text, Required: true, "The name in English.", "الاسم بالإنجليزية."),
        new(ParentCodeColumn, ImportValueKind.Code, Required: false,
            "The parent department's code: an active department, or a row above this one. Empty for a top-level department.",
            "رمز الإدارة الأم: إدارة نشطة أو صف يسبق هذا الصف. يُترك فارغًا للإدارة العليا."),
        new(DirectoryReferenceColumn, ImportValueKind.Text, Required: false,
            "The directory (AD) object this department stands for; sign-in resolves a user's department by it.",
            "كائن الدليل (AD) الذي تمثله الإدارة؛ يُحدَّد به قسم المستخدم عند الدخول."),
        new(IsActiveColumn, ImportValueKind.Boolean, Required: false, "FALSE for a department no longer in use. Empty means TRUE.", "FALSE للإدارة غير المستخدمة. الفارغ يعني TRUE."),
    ];

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>>> ReferenceValuesAsync(CancellationToken cancellationToken) =>
        new Dictionary<string, IReadOnlyList<ImportReferenceValue>>
        {
            [ParentCodeColumn] = [.. (await directory.ListActiveDepartmentCodesAsync(cancellationToken).ConfigureAwait(false)).Select(d => new ImportReferenceValue(d.Code, d.Name.Ar, d.Name.En))],
        };

    public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);

        Guid? parentId = null;
        if (record.Text(ParentCodeColumn) is { } parentCode
            && (parentId = await directory.FindDepartmentIdAsync(parentCode, cancellationToken).ConfigureAwait(false)) is null)
        {
            return ImportRowResult.Rejected(ParentCodeColumn, ImportIssueCodes.ReferenceNotFound);
        }

        if (await directory.FindDepartmentIdAsync(record.Text(CodeColumn)!, cancellationToken).ConfigureAwait(false) is not null)
        {
            return ImportRowResult.Rejected(CodeColumn, ImportIssueCodes.AlreadyExists);
        }

        DepartmentDraft draft = new(record.Text(CodeColumn)!, new BilingualLabel(record.Text(NameArColumn)!, record.Text(NameEnColumn)!), parentId, record.Text(DirectoryReferenceColumn));
        AdministrationResult<Versioned<DepartmentDetail>> created = await departments.CreateAsync(context.ActorId, draft, cancellationToken).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            return AdministrationImportIssues.Rejected(created.Error, ColumnOfField);
        }

        Guid departmentId = created.Value.Value.Id;
        return record.Boolean(IsActiveColumn) == false
               && (await departments.DeactivateAsync(context.ActorId, departmentId, expectedVersion: null, cancellationToken).ConfigureAwait(false)).Error is { } deactivation
            ? AdministrationImportIssues.Rejected(deactivation, ColumnOfField)
            : ImportRowResult.Created(departmentId);
    }
}
