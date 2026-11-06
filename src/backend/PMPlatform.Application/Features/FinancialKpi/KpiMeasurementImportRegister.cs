using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// A project's KPI figures from a workbook, each a DRAFT through <see cref="IKpiMeasurementService.CreateAsync"/> as the form's: the project's team
/// still submits it for review. A row names the KPI by its code, on one of the project's ACTIVE assignments.
/// </summary>
internal sealed class KpiMeasurementImportRegister(
    IKpiMeasurementService measurements, IFinancialKpiRepository repository, KpiDefinitions definitions, ProjectImportScope scope) : IImportRegister
{
    private const string KpiColumn = "kpi_code";
    private const string PeriodStartColumn = "period_start";
    private const string PeriodEndColumn = "period_end";
    private const string ValueColumn = "measured_value";
    private const string StatusColumn = "value_status";
    private const string AsOfColumn = "as_of_date";
    private const string NarrativeColumn = "narrative";
    private const int ValuePlaces = 4;
    private const decimal ValueLimit = 100_000_000_000_000m;

    private static readonly Dictionary<string, string> ColumnOfField = new(StringComparer.Ordinal)
    {
        ["periodEnd"] = PeriodEndColumn,
        ["measuredValue"] = ValueColumn,
        ["valueStatus"] = StatusColumn,
        ["asOfDate"] = AsOfColumn,
    };

    public string Code => ImportRegisterCodes.KpiMeasurement;

    public string OwningModule => "FinancialKpi";

    public string Permission => PermissionCatalogue.KpiRecord;

    public IReadOnlyList<ImportColumn> Columns { get; } =
    [
        ProjectImportScope.Column,
        new(KpiColumn, ImportValueKind.Code, Required: true, "The code of a KPI assigned to the project.", "رمز مؤشر مسند إلى المشروع."),
        new(PeriodStartColumn, ImportValueKind.Date, Required: true, "The first day of the period measured.", "أول يوم في الفترة المقيسة."),
        new(PeriodEndColumn, ImportValueKind.Date, Required: true, "The last day of the period measured.", "آخر يوم في الفترة المقيسة."),
        ImportColumn.ChoiceOf<ValueStatus>(StatusColumn, required: true, "MEASURED with a value, or why there is none.", "MEASURED مع قيمة، أو سبب عدم وجودها."),
        new(ValueColumn, ImportValueKind.Number, Required: false, "The figure, at most four decimal places. Empty unless MEASURED: never 0 for unknown.", "القيمة بأربع منازل عشرية كحد أقصى. فارغة ما لم تكن MEASURED: لا تُكتب 0 لغير المعروف."),
        new(AsOfColumn, ImportValueKind.Date, Required: true, "The date the figure is as of.", "تاريخ القيمة."),
        new(NarrativeColumn, ImportValueKind.Text, Required: false, "A note on the figure.", "ملاحظة على القيمة.") { MaxLength = 2000 },
        ProjectImportScope.Language,
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
        };

    public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);

        if (record.Number(ValueColumn) is { } value && (decimal.Round(value, ValuePlaces) != value || Math.Abs(value) >= ValueLimit))
        {
            return ImportRowResult.Rejected(ValueColumn, ValidationCode.OutOfRange);
        }

        List<Guid> assignments = [];
        foreach (KpiAssignment assignment in await repository.ListActiveAssignmentsAsync([record.ProjectId!.Value], cancellationToken).ConfigureAwait(false))
        {
            if ((await definitions.RequireAsync(assignment.KpiDefinitionId, cancellationToken).ConfigureAwait(false)).Code == record.Text(KpiColumn))
            {
                assignments.Add(assignment.Id);
            }
        }

        if (assignments.Count != 1)
        {
            return ImportRowResult.Rejected(KpiColumn, ImportIssueCodes.ReferenceNotFound);
        }

        KpiMeasurementDraft draft = new(
            assignments[0],
            record.Date(PeriodStartColumn)!.Value,
            record.Date(PeriodEndColumn)!.Value,
            record.Number(ValueColumn),
            record.Choice<ValueStatus>(StatusColumn)!.Value,
            record.Date(AsOfColumn)!.Value,
            ProjectImportScope.Narrative(record, NarrativeColumn));
        AdministrationResult<Versioned<KpiMeasurementDetail>> created = await measurements.CreateAsync(context.ActorId, draft, cancellationToken).ConfigureAwait(false);
        return created.Succeeded ? ImportRowResult.Created(created.Value.Value.Id) : AdministrationImportIssues.Rejected(created.Error, ColumnOfField);
    }
}
