using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.Contracts.Events;
using PMPlatform.Application.Features.Project.EventHandlers;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project;

/// <summary>
/// WF-01's migration workbook (TASK-102): each row is a project already under way, taken in by ADR-014's intake — ACTIVE, with its Formal
/// Project ID, its manager, a Declared Baseline and its opening position — and checked by the registration's own reference rules. The intake
/// is published as <see cref="ProjectIntakeRecorded"/>, from which Schedule, Progress and FinancialKpi each write their own fact after the
/// import commits (edge 35). A manager who does not yet hold R04 over the project is given it on the project, through IdentityAccess's
/// own assignment command, as ADM-010 would (data-import.md D-10).
/// </summary>
internal sealed class ProjectImportRegister(
    IProjectRepository repository,
    ProjectReferences references,
    ProjectManagerEligibility managers,
    IAccessRelationshipService assignments,
    IPermissionProfileDirectory profiles,
    IOrganizationDirectory directory,
    IUserLookup users,
    CatalogueCodeBook catalogues,
    IOutbox outbox,
    IAuditTrail audit,
    IAuditRequestContext request,
    TimeProvider timeProvider) : IImportRegister
{
    /// <summary>The profile an imported project's manager is given on the project when they hold no R04 over it yet.</summary>
    public const string ManagerProfileCode = "R04-DEFAULT";

    private const string FormalIdColumn = "formal_project_id";
    private const string TitleColumn = "title";
    private const string DescriptionColumn = "description";
    private const string LanguageColumn = "text_language";
    private const string ClassificationColumn = "classification_code";
    private const string DepartmentColumn = "department_code";
    private const string EntityColumn = "external_entity_code";
    private const string ParticipationColumn = "participation_mode";
    private const string GovernanceProfileColumn = "governance_profile_code";
    private const string ManagerColumn = "project_manager_username";
    private const string SponsorColumn = "manager_sponsor_username";
    private const string BudgetColumn = "registration_budget_sar";
    private const string PlannedStartColumn = "planned_start_date";
    private const string PlannedEndColumn = "planned_end_date";
    private const string RegionColumn = "region_code";
    private const string CityColumn = "city_code";
    private const string LatitudeColumn = "latitude";
    private const string LongitudeColumn = "longitude";
    private const string IntakeDateColumn = "intake_date";
    private const string ScopeColumn = "declared_scope";
    private const string DeclaredBudgetColumn = "declared_budget_sar";
    private const string DeclaredEndColumn = "declared_end_date";
    private const string PercentColumn = "opening_percent_complete";
    private const string SpendColumn = "opening_spend_to_date_sar";

    private const int NarrativeLength = 2000;

    private static readonly Dictionary<string, string> ColumnOfField = new(StringComparer.Ordinal)
    {
        ["classificationItemId"] = ClassificationColumn,
        ["departmentId"] = DepartmentColumn,
        ["externalEntityId"] = EntityColumn,
        ["governanceProfileItemId"] = GovernanceProfileColumn,
        ["regionItemId"] = RegionColumn,
        ["cityItemId"] = CityColumn,
        ["userId"] = ManagerColumn,
        ["sponsorUserId"] = SponsorColumn,
    };

    public string Code => ImportRegisterCodes.Project;

    public string OwningModule => "Project";

    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        new(FormalIdColumn, ImportValueKind.Text, Required: false,
            "The project's identifier in the legacy system, kept as its Formal Project ID. Empty: the platform issues one (PRJ-nnnnnn).",
            "معرّف المشروع في النظام السابق، يُحفظ معرّفًا رسميًا للمشروع. الفارغ: تُصدر المنصة معرّفًا (PRJ-nnnnnn).") { MaxLength = 50 },
        new(TitleColumn, ImportValueKind.Text, Required: true, "The project's title.", "عنوان المشروع.") { MaxLength = NarrativeLength },
        new(DescriptionColumn, ImportValueKind.Text, Required: false, "The project's description.", "وصف المشروع.") { MaxLength = NarrativeLength },
        new(LanguageColumn, ImportValueKind.Choice, Required: true,
            "The language the title, description and declared scope are written in: ar or en.", "لغة العنوان والوصف والنطاق المعلن: ar أو en.") { Choices = ["ar", "en"] },
        new(ClassificationColumn, ImportValueKind.Code, Required: true, "The project classification's code.", "رمز تصنيف المشروع."),
        new(DepartmentColumn, ImportValueKind.Code, Required: true, "The owning department's code: an active department.", "رمز الإدارة المالكة: إدارة نشطة."),
        new(EntityColumn, ImportValueKind.Code, Required: false,
            "The delivering entity's code, when an external entity delivers the project.", "رمز الجهة المنفذة إذا نفذت المشروع جهة خارجية."),
        ImportColumn.ChoiceOf<ParticipationMode>(ParticipationColumn, required: true,
            "Who manages the day-to-day record. ENTITY_MANAGED needs a delivering entity.", "من يدير السجل اليومي. ENTITY_MANAGED يتطلب جهة منفذة."),
        new(GovernanceProfileColumn, ImportValueKind.Code, Required: true, "The governance profile's code.", "رمز ملف الحوكمة."),
        new(ManagerColumn, ImportValueKind.Text, Required: true,
            "The project manager's username. If they do not yet hold R04 over the project, they are given it on this project.",
            "اسم مستخدم مدير المشروع. إن لم يكن يحمل الدور R04 على المشروع يُمنح له على هذا المشروع.") { MaxLength = 100 },
        new(SponsorColumn, ImportValueKind.Text, Required: false,
            "Required when the manager is an external user given R04 by this import: the AHDA sponsor of that access (ADR-013).",
            "إلزامي إذا كان المدير مستخدمًا خارجيًا يُمنح الدور R04 بهذا الاستيراد: راعي هذا الوصول في الهيئة.") { MaxLength = 100 },
        new(BudgetColumn, ImportValueKind.Money, Required: false, "The budget stated at registration, in SAR.", "الميزانية المذكورة عند التسجيل بالريال."),
        new(PlannedStartColumn, ImportValueKind.Date, Required: false, "The planned start date.", "تاريخ البداية المخطط."),
        new(PlannedEndColumn, ImportValueKind.Date, Required: false, "The planned end date, not before the start.", "تاريخ النهاية المخطط، لا يسبق البداية."),
        new(RegionColumn, ImportValueKind.Code, Required: false, "The region's code.", "رمز المنطقة."),
        new(CityColumn, ImportValueKind.Code, Required: false, "The city's code, in the region given.", "رمز المدينة، ضمن المنطقة المذكورة."),
        new(LatitudeColumn, ImportValueKind.Number, Required: false, "Latitude, -90 to 90.", "خط العرض، من -90 إلى 90."),
        new(LongitudeColumn, ImportValueKind.Number, Required: false, "Longitude, -180 to 180.", "خط الطول، من -180 إلى 180."),
        new(IntakeDateColumn, ImportValueKind.Date, Required: true,
            "The date the project is taken in: its opening position is as of this date. Not in the future.",
            "تاريخ إدخال المشروع: وضعه الافتتاحي في هذا التاريخ. لا يكون في المستقبل."),
        new(ScopeColumn, ImportValueKind.Text, Required: true, "The scope as declared at intake (the Declared Baseline).", "النطاق كما أُعلن عند الإدخال (خط الأساس المعلن).") { MaxLength = NarrativeLength },
        new(DeclaredBudgetColumn, ImportValueKind.Money, Required: true, "The declared budget, in SAR.", "الميزانية المعلنة بالريال."),
        new(DeclaredEndColumn, ImportValueKind.Date, Required: true, "The declared end date.", "تاريخ النهاية المعلن."),
        new(PercentColumn, ImportValueKind.Number, Required: true, "Percent complete at intake, 0 to 100.", "نسبة الإنجاز عند الإدخال، من 0 إلى 100."),
        new(SpendColumn, ImportValueKind.Money, Required: true, "Spend to date at intake, in SAR.", "الصرف حتى تاريخ الإدخال بالريال."),
    ];

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>>> ReferenceValuesAsync(CancellationToken cancellationToken) =>
        new Dictionary<string, IReadOnlyList<ImportReferenceValue>>
        {
            [ClassificationColumn] = await catalogues.ReferenceValuesAsync(MasterDataCatalogueCodes.ProjectClassification, cancellationToken).ConfigureAwait(false),
            [DepartmentColumn] = Values(await directory.ListActiveDepartmentCodesAsync(cancellationToken).ConfigureAwait(false)),
            [EntityColumn] = Values(await directory.ListActiveExternalEntityCodesAsync(cancellationToken).ConfigureAwait(false)),
            [GovernanceProfileColumn] = await catalogues.ReferenceValuesAsync(MasterDataCatalogueCodes.GovernanceProfile, cancellationToken).ConfigureAwait(false),
            [RegionColumn] = await catalogues.ReferenceValuesAsync(MasterDataCatalogueCodes.Region, cancellationToken).ConfigureAwait(false),
            [CityColumn] = await catalogues.ReferenceValuesAsync(MasterDataCatalogueCodes.City, cancellationToken).ConfigureAwait(false),
        };

    public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);

        List<ImportCellIssue> issues = [];
        Guid? classification = await ItemAsync(MasterDataCatalogueCodes.ProjectClassification, record, ClassificationColumn, issues, cancellationToken).ConfigureAwait(false);
        Guid? governanceProfile = await ItemAsync(MasterDataCatalogueCodes.GovernanceProfile, record, GovernanceProfileColumn, issues, cancellationToken).ConfigureAwait(false);
        Guid? region = await ItemAsync(MasterDataCatalogueCodes.Region, record, RegionColumn, issues, cancellationToken).ConfigureAwait(false);
        Guid? city = await ItemAsync(MasterDataCatalogueCodes.City, record, CityColumn, issues, cancellationToken).ConfigureAwait(false);
        Guid? department = await Found(directory.FindDepartmentIdAsync(record.Text(DepartmentColumn)!, cancellationToken), DepartmentColumn, issues).ConfigureAwait(false);
        Guid? entity = record.Text(EntityColumn) is { } entityCode
            ? await Found(directory.FindExternalEntityIdAsync(entityCode, cancellationToken), EntityColumn, issues).ConfigureAwait(false)
            : null;
        Guid? manager = await Found(users.FindIdByUsernameAsync(record.Text(ManagerColumn)!, cancellationToken), ManagerColumn, issues).ConfigureAwait(false);
        Guid? sponsor = record.Text(SponsorColumn) is { } sponsorName
            ? await Found(users.FindIdByUsernameAsync(sponsorName, cancellationToken), SponsorColumn, issues).ConfigureAwait(false)
            : null;
        if (record.Text(FormalIdColumn) is { } formalId && await repository.FindIdByFormalProjectIdAsync(formalId, cancellationToken).ConfigureAwait(false) is not null)
        {
            issues.Add(new ImportCellIssue(FormalIdColumn, ImportIssueCodes.AlreadyExists));
        }

        issues.AddRange(ShapeIssues(record, DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)));
        if (issues.Count > 0)
        {
            return ImportRowResult.Rejected(issues);
        }

        Language language = LanguageCode.Parse(record.Text(LanguageColumn)!);
        ProjectDraft draft = new(
            new NarrativeText(record.Text(TitleColumn)!, language),
            record.Text(DescriptionColumn) is { } description ? new NarrativeText(description, language) : null,
            classification!.Value,
            department!.Value,
            entity,
            record.Choice<ParticipationMode>(ParticipationColumn)!.Value,
            governanceProfile!.Value,
            record.Money(BudgetColumn),
            record.Date(PlannedStartColumn),
            record.Date(PlannedEndColumn),
            region,
            city,
            record.Number(LatitudeColumn),
            record.Number(LongitudeColumn));
        if (await references.CheckAsync(draft, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return AdministrationImportIssues.Rejected(invalid, ColumnOfField);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProjectEntity project = new()
        {
            Id = Guid.CreateVersion7(now),
            Title = draft.Title,
            LifecycleState = ProjectLifecycleState.Active,
            RevisionNo = 1,
            FormalProjectId = record.Text(FormalIdColumn)
                              ?? ProjectApprovalOutcomeHandler.FormalProjectIdOf(await repository.NextFormalProjectNumberAsync(cancellationToken).ConfigureAwait(false)),
            ProjectManagerUserId = manager,
            LegacyIntakeDate = record.Date(IntakeDateColumn),
            ActivatedAt = now,
            CreatedAt = now,
            CreatedBy = context.ActorId,
            UpdatedAt = now,
            UpdatedBy = context.ActorId,
        };
        ProjectMapping.Apply(draft, project);
        ProjectIntake intake = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            IntakeDate = record.Date(IntakeDateColumn)!.Value,
            DeclaredScope = new NarrativeText(record.Text(ScopeColumn)!, language),
            DeclaredBudgetSar = record.Money(DeclaredBudgetColumn)!.Value,
            DeclaredEndDate = record.Date(DeclaredEndColumn)!.Value,
            OpeningPercentComplete = record.Number(PercentColumn)!.Value,
            OpeningSpendToDateSar = record.Money(SpendColumn)!.Value,
            RecordedByUserId = context.ActorId,
            CreatedAt = now,
            CreatedBy = context.ActorId,
            UpdatedAt = now,
            UpdatedBy = context.ActorId,
        };
        repository.Add(project);
        repository.Add(intake);
        audit.Stage(ProjectAudit.TakenIn(context.ActorId, project, intake));
        outbox.Stage(IntakeRecorded(project, intake, context.ActorId, now));
        switch (await repository.SaveAsync(cancellationToken).ConfigureAwait(false))
        {
            case ProjectSaveOutcome.Saved:
                break;
            case ProjectSaveOutcome.ConcurrencyConflict:
                // The one unique key a new project meets is its Formal Project ID, taken since it was checked.
                return ImportRowResult.Rejected(FormalIdColumn, ImportIssueCodes.AlreadyExists);
            case ProjectSaveOutcome.InUse:
            default:
                throw new InvalidOperationException("Saving an imported project failed.");
        }

        return await managers.IsEligibleAsync(manager!.Value, project, cancellationToken).ConfigureAwait(false)
            ? ImportRowResult.Created(project.Id)
            : await AssignManagerAsync(context.ActorId, project, sponsor, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>R04 on the project for its manager, as ADM-010 grants it: an external manager needs a sponsor (ADR-013).</summary>
    private async Task<ImportRowResult> AssignManagerAsync(Guid actorId, ProjectEntity project, Guid? sponsor, CancellationToken cancellationToken)
    {
        Guid profileVersion = await profiles.FindPublishedVersionIdAsync(ManagerProfileCode, cancellationToken).ConfigureAwait(false)
                              ?? throw new InvalidOperationException($"Profile {ManagerProfileCode} has no PUBLISHED version.");
        AccessRelationshipDraft grant = new(project.ProjectManagerUserId!.Value, profileVersion, null, project.ExternalEntityId, project.Id, sponsor, null, null);
        AdministrationResult<AccessRelationshipDetail> assigned = await assignments.CreateAsync(actorId, grant, cancellationToken).ConfigureAwait(false);
        return !assigned.Succeeded ? AdministrationImportIssues.Rejected(assigned.Error, ColumnOfField)
            : await managers.IsEligibleAsync(project.ProjectManagerUserId.Value, project, cancellationToken).ConfigureAwait(false) ? ImportRowResult.Created(project.Id)
            : ImportRowResult.Rejected(ManagerColumn, ValidationCode.NotAllowed);
    }

    private ProjectIntakeRecorded IntakeRecorded(ProjectEntity project, ProjectIntake intake, Guid actorId, DateTimeOffset now) => new()
    {
        EventId = Guid.CreateVersion7(now),
        EventType = ProjectIntakeRecorded.Type,
        SchemaVersion = ProjectIntakeRecorded.CurrentSchemaVersion,
        Kind = EventKind.DomainEvent,
        MessageKey = EventMessageKey.Of(ProjectIntakeRecorded.Type, intake.Id.ToString("D")),
        IdempotencyKey = intake.Id.ToString("D"),
        OccurredAt = now,
        SourceModule = OwningModule,
        CorrelationId = request.CorrelationId,
        Actor = new EventActor(AuditActorType.User, actorId),
        Subject = new EventSubject(OwningModule, nameof(ProjectIntake), intake.Id, null),
        Scope = new EventScope(project.Id, project.DepartmentId, project.ExternalEntityId),
        Data = new ProjectIntakeRecordedData(
            project.Id,
            intake.Id,
            intake.IntakeDate,
            new DeclaredBaselineData(intake.DeclaredBudgetSar.Amount, intake.DeclaredEndDate, intake.DeclaredScope),
            new OpeningPositionData(intake.OpeningPercentComplete, intake.OpeningSpendToDateSar.Amount, []),
            actorId),
    };

    /// <summary>What the API's request model checks (MOD-001), and the intake's own bounds.</summary>
    private static IEnumerable<ImportCellIssue> ShapeIssues(ImportRecord record, DateOnly today)
    {
        if (record.Date(PlannedStartColumn) is { } start && record.Date(PlannedEndColumn) is { } end && end < start)
        {
            yield return new ImportCellIssue(PlannedEndColumn, ValidationCode.DateBeforeStart);
        }

        if (record.Number(LatitudeColumn) is < -90 or > 90)
        {
            yield return new ImportCellIssue(LatitudeColumn, ValidationCode.OutOfRange);
        }

        if (record.Number(LongitudeColumn) is < -180 or > 180)
        {
            yield return new ImportCellIssue(LongitudeColumn, ValidationCode.OutOfRange);
        }

        if (record.Date(IntakeDateColumn) > today)
        {
            yield return new ImportCellIssue(IntakeDateColumn, ValidationCode.OutOfRange);
        }

        if (record.Number(PercentColumn) is < 0 or > 100)
        {
            yield return new ImportCellIssue(PercentColumn, ValidationCode.OutOfRange);
        }
    }

    private async Task<Guid?> ItemAsync(string catalogueCode, ImportRecord record, string column, List<ImportCellIssue> issues, CancellationToken cancellationToken)
    {
        if (record.Text(column) is not { } code)
        {
            return null;
        }

        MasterDataItemReference? item = await catalogues.FindAsync(catalogueCode, code, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            issues.Add(new ImportCellIssue(column, ImportIssueCodes.ReferenceNotFound));
        }

        return item?.Id;
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

    private static List<ImportReferenceValue> Values(IReadOnlyList<OrganizationCode> codes) =>
        [.. codes.Select(c => new ImportReferenceValue(c.Code, c.Name.Ar, c.Name.En))];
}
