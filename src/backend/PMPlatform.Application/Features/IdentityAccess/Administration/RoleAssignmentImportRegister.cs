using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// ADM-010's migration workbook (TASK-102): who holds which role, and over what, each row through <see cref="IAccessRelationshipService"/>
/// with ADM-010's rules — bound to the profile's PUBLISHED version (ADR-018), an external user only through an external-eligible role on
/// their own entity with a sponsor (ADR-013), never to the importer themselves. A project is named by the legacy ID it was imported under,
/// so this register loads after projects. An assignment starts when it is imported: access is never backdated.
/// </summary>
internal sealed class RoleAssignmentImportRegister(
    IAccessRelationshipService assignments, IPermissionProfileDirectory profiles, IUserLookup users, IOrganizationDirectory directory) : IImportRegister
{
    private const string UsernameColumn = "username";
    private const string ProfileColumn = "profile_code";
    private const string DepartmentColumn = "department_code";
    private const string EntityColumn = "external_entity_code";
    private const string ProjectColumn = "project_legacy_id";
    private const string SponsorColumn = "sponsor_username";
    private const string EndsOnColumn = "ends_on";

    private static readonly Dictionary<string, string> ColumnOfField = new(StringComparer.Ordinal)
    {
        ["userId"] = UsernameColumn,
        ["permissionProfileVersionId"] = ProfileColumn,
        ["departmentId"] = DepartmentColumn,
        ["externalEntityId"] = EntityColumn,
        ["projectId"] = ProjectColumn,
        ["sponsorUserId"] = SponsorColumn,
        ["endsAt"] = EndsOnColumn,
    };

    public string Code => ImportRegisterCodes.RoleAssignment;

    public string OwningModule => "IdentityAccess";

    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        new(UsernameColumn, ImportValueKind.Text, Required: true, "The username of the person given the role.", "اسم مستخدم من يُمنح الدور.") { MaxLength = 100 },
        new(ProfileColumn, ImportValueKind.Code, Required: true,
            "The permission profile's code, e.g. R03-DEFAULT: its PUBLISHED version is assigned.", "رمز ملف الصلاحيات، مثل R03-DEFAULT: يُسند إصداره المنشور.") { MaxLength = 100 },
        new(DepartmentColumn, ImportValueKind.Code, Required: false, "The department the role is held over, if scoped to one.", "الإدارة التي يُحمل الدور عليها إن كان مقيدًا بها."),
        new(EntityColumn, ImportValueKind.Code, Required: false,
            "The entity the role is held over. An external user's is always their own entity.", "الجهة التي يُحمل الدور عليها. للمستخدم الخارجي هي جهته دائمًا."),
        new(ProjectColumn, ImportValueKind.Text, Required: false,
            "The legacy ID of an imported project the role is held on, if scoped to one. An external R04 is always per project.",
            "معرّف مشروع مستورد في النظام السابق إن كان الدور على مشروع واحد. R04 الخارجي يكون لكل مشروع دائمًا.") { MaxLength = ImportColumn.LegacyIdLength },
        new(SponsorColumn, ImportValueKind.Text, Required: false, "Required for an external user: the AHDA sponsor of the access.", "إلزامي للمستخدم الخارجي: راعي الوصول في الهيئة.") { MaxLength = 100 },
        new(EndsOnColumn, ImportValueKind.Date, Required: false, "The last day the role is held, if it ends.", "آخر يوم يُحمل فيه الدور إن كان له نهاية."),
    ];

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>>> ReferenceValuesAsync(CancellationToken cancellationToken) =>
        new Dictionary<string, IReadOnlyList<ImportReferenceValue>>
        {
            [ProfileColumn] = [.. (await profiles.ListAssignableAsync(cancellationToken).ConfigureAwait(false)).Select(p => new ImportReferenceValue(p.Code, p.Name.Ar, p.Name.En))],
            [DepartmentColumn] = [.. (await directory.ListActiveDepartmentCodesAsync(cancellationToken).ConfigureAwait(false)).Select(d => new ImportReferenceValue(d.Code, d.Name.Ar, d.Name.En))],
            [EntityColumn] = [.. (await directory.ListActiveExternalEntityCodesAsync(cancellationToken).ConfigureAwait(false)).Select(e => new ImportReferenceValue(e.Code, e.Name.Ar, e.Name.En))],
        };

    public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);

        List<ImportCellIssue> issues = [];
        Guid? user = await Found(users.FindIdByUsernameAsync(record.Text(UsernameColumn)!, cancellationToken), UsernameColumn, issues).ConfigureAwait(false);
        Guid? profileVersion = await Found(profiles.FindPublishedVersionIdAsync(record.Text(ProfileColumn)!, cancellationToken), ProfileColumn, issues).ConfigureAwait(false);
        Guid? department = record.Text(DepartmentColumn) is { } departmentCode
            ? await Found(directory.FindDepartmentIdAsync(departmentCode, cancellationToken), DepartmentColumn, issues).ConfigureAwait(false)
            : null;
        Guid? entity = record.Text(EntityColumn) is { } entityCode
            ? await Found(directory.FindExternalEntityIdAsync(entityCode, cancellationToken), EntityColumn, issues).ConfigureAwait(false)
            : null;
        Guid? project = record.Text(ProjectColumn) is { } projectLegacyId
            ? await Found(context.FindImportedAsync(ImportRegisterCodes.Project, projectLegacyId, cancellationToken), ProjectColumn, issues).ConfigureAwait(false)
            : null;
        Guid? sponsor = record.Text(SponsorColumn) is { } sponsorName
            ? await Found(users.FindIdByUsernameAsync(sponsorName, cancellationToken), SponsorColumn, issues).ConfigureAwait(false)
            : null;
        if (issues.Count > 0)
        {
            return ImportRowResult.Rejected(issues);
        }

        // The last day held: the assignment ends at the start of the next, in UTC as every period the platform keeps.
        DateTimeOffset? endsAt = record.Date(EndsOnColumn) is { } endsOn ? new DateTimeOffset(endsOn.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;
        AccessRelationshipDraft draft = new(user!.Value, profileVersion!.Value, department, entity, project, sponsor, null, endsAt);
        AdministrationResult<AccessRelationshipDetail> created = await assignments.CreateAsync(context.ActorId, draft, cancellationToken).ConfigureAwait(false);
        return created.Succeeded ? ImportRowResult.Created(created.Value.Id) : AdministrationImportIssues.Rejected(created.Error, ColumnOfField);
    }

    private static async Task<Guid?> Found(Task<Guid?> lookup, string column, List<ImportCellIssue> issues)
    {
        Guid? id = await lookup.ConfigureAwait(false);
        if (id is null)
        {
            issues.Add(new ImportCellIssue(column, ImportIssueCodes.ReferenceNotFound));
        }

        return id;
    }
}
