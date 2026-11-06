using System.Globalization;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;
using static PMPlatform.Application.Features.Project.Import.ProjectImportLayout;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project.Import;

/// <summary>
/// One workbook row as an approved batch would apply it (R3-4): the project it creates or the project it updates, with every value resolved.
/// Blanks stay blank — progress and spend are never set to zero.
/// </summary>
internal sealed record ResolvedRow(
    int Row,
    string LegacyReference,
    ProjectImportRowAction Action,
    ProjectEntity? Existing,
    NarrativeText Title,
    string? TitleEn,
    bool? IsStrategic,
    NarrativeText? Description,
    Guid ExternalEntityId,
    Guid DepartmentId,
    Guid ClassificationItemId,
    Guid StrategicPortfolioItemId,
    Guid? StrategicInitiativeItemId,
    Guid? FollowUpOwnerUserId,
    Guid ProjectManagerUserId,
    ProjectLifecycleState Status,
    Guid GovernanceProfileItemId,
    Guid? PriorityItemId,
    Money Budget,
    FundingSource? Funding,
    DateOnly Start,
    DateOnly End,
    Guid RegionItemId,
    Guid CityItemId,
    decimal? PlannedPercent,
    decimal? ActualPercent,
    decimal? PlannedSpendPercent,
    decimal? ActualSpendPercent,
    NarrativeText? Obstacle,
    NarrativeText? NextAction,
    string? ActionOwner,
    DateOnly? ActionDue,
    bool? EntityCompliant,
    NarrativeText? Note);

/// <summary>What a workbook would do, and the rows an approval applies (those with no error).</summary>
internal sealed record ProjectImportEvaluation(ProjectImportPreview Preview, IReadOnlyList<ResolvedRow> Rows);

/// <summary>
/// The validation of RPMO's workbook (review §4, Validation rules): each row's cells by their column's shape, each name against its list, then
/// the row's own rules. An error blocks the row; a warning is allowed but flagged; info is what the platform settled itself.
/// </summary>
internal static class ProjectImportEvaluator
{
    private const decimal ExceptionalValueSar = 10_000_000_000m;

    /// <summary>The input a validation needs besides the sheet: who imports and where, and the projects the rows may match.</summary>
    public sealed record Context(
        ProjectImportReferences References,
        Func<Guid, Guid?, Guid?, bool> InScope,
        Func<Guid, bool> Reaches,
        Func<Guid, Guid?, bool> IsProjectManager,
        IReadOnlyDictionary<string, ProjectEntity> ByLegacyReference,
        IReadOnlyDictionary<string, ProjectEntity> ByFormalId,
        IReadOnlyCollection<(string Title, Guid? Entity)> ExistingNames,
        ProjectImportPlan Plan,
        DateOnly Today);

    /// <summary>The key row: the first row holding <see cref="LegacyRef"/>, whose cells name each column.</summary>
    public static (int KeyRow, Dictionary<int, string> Keys, int? Version)? KeysOf(ImportSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        foreach (ImportSheetRow row in sheet.Rows)
        {
            if (row.Cells.Values.Any(v => string.Equals(v.Trim(), LegacyRef, StringComparison.Ordinal)))
            {
                Dictionary<int, string> keys = [];
                int? version = null;
                foreach ((int index, string text) in row.Cells)
                {
                    string key = text.Trim();
                    if (key.StartsWith(VersionKeyPrefix, StringComparison.Ordinal))
                    {
                        version = int.TryParse(key[VersionKeyPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out int v) ? v : 0;
                    }
                    else
                    {
                        keys[index] = key;
                    }
                }

                return (row.RowNumber, keys, version);
            }
        }

        return null;
    }

    public static ProjectImportEvaluation Evaluate(ImportSheet sheet, string sha256, Context context)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(context);

        (int keyRow, Dictionary<int, string> keys, int? version) = KeysOf(sheet)!.Value;
        List<ProjectImportIssue> fileIssues = [];
        if (version is { } stamped && stamped != TemplateVersion)
        {
            fileIssues.Add(new ProjectImportIssue(string.Empty, ProjectImportIssueCodes.TemplateOutdated, ProjectImportIssueLevel.Warning));
        }

        HashSet<string> known = [.. Columns.Select(c => c.Column.Key)];
        fileIssues.AddRange(keys.Values.Where(k => !known.Contains(k)).Distinct(StringComparer.Ordinal)
            .Select(k => new ProjectImportIssue(k, ProjectImportIssueCodes.ColumnIgnored, ProjectImportIssueLevel.Info)));
        fileIssues.AddRange(Columns.Where(c => c.Column.Required && !keys.ContainsValue(c.Column.Key))
            .Select(c => new ProjectImportIssue(c.Column.Key, ProjectImportIssueCodes.ColumnMissing, ProjectImportIssueLevel.Error)));

        Dictionary<string, int> indexOf = [];
        foreach ((int index, string key) in keys.Where(k => known.Contains(k.Value)))
        {
            indexOf.TryAdd(key, index);
        }

        HashSet<string> skipped = new(context.Plan.SkippedRows, StringComparer.Ordinal);
        Dictionary<string, int> legacyCount = new(StringComparer.Ordinal);
        List<ImportSheetRow> rows = [.. sheet.Rows.Where(r => r.RowNumber > keyRow && HasProject(r, indexOf))];
        foreach (ImportSheetRow row in rows)
        {
            if (Cell(row, indexOf, LegacyRef) is { } reference)
            {
                legacyCount[reference] = legacyCount.GetValueOrDefault(reference) + 1;
            }
        }

        Dictionary<(ProjectImportDimension, string), int> unmatched = [];
        List<ProjectImportRowPreview> previews = [];
        List<ResolvedRow> resolved = [];
        foreach (ImportSheetRow row in rows)
        {
            RowEvaluation evaluation = new(row, indexOf, context, unmatched);
            ResolvedRow? result = evaluation.Run(legacyCount, skipped);
            previews.Add(evaluation.Preview);
            if (result is not null)
            {
                resolved.Add(result);
            }
        }

        int errors = previews.Count(p => p.Issues.Any(i => i.Level == ProjectImportIssueLevel.Error)) + fileIssues.Count(i => i.Level == ProjectImportIssueLevel.Error);
        List<ProjectImportUnmatchedName> names = [.. unmatched
            .OrderBy(u => u.Key.Item1).ThenBy(u => u.Key.Item2, StringComparer.Ordinal)
            .Select(u => new ProjectImportUnmatchedName(u.Key.Item1, u.Key.Item2, u.Value, context.References.Options(u.Key.Item1)))];
        List<ProjectImportRowPreview> ready = [.. previews.Where(p => p.Issues.All(i => i.Level != ProjectImportIssueLevel.Error))];
        ProjectImportPreview preview = new(
            sha256,
            previews.Count,
            ready.Count(p => p.Action == ProjectImportRowAction.Create),
            ready.Count(p => p.Action == ProjectImportRowAction.Update),
            ready.Count(p => p.Action == ProjectImportRowAction.Skip),
            errors,
            previews.Count(p => p.Issues.Any(i => i.Level == ProjectImportIssueLevel.Warning)),
            Submittable: errors == 0 && names.All(n => n.Dimension == ProjectImportDimension.FollowUpOwner) && previews.Count > 0,
            fileIssues,
            names,
            previews);
        return new ProjectImportEvaluation(preview, resolved);
    }

    /// <summary>A prepared but empty template row has no legacy reference and no name: it is not a project.</summary>
    private static bool HasProject(ImportSheetRow row, Dictionary<string, int> indexOf) =>
        Cell(row, indexOf, LegacyRef) is not null || Cell(row, indexOf, NameAr) is not null;

    private static string? Cell(ImportSheetRow row, Dictionary<string, int> indexOf, string key) =>
        indexOf.TryGetValue(key, out int index) && row.Cells.TryGetValue(index, out string? value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    /// <summary>One row's evaluation: its issues, as they are found, and the row it resolves to when it has no error.</summary>
    private sealed class RowEvaluation(
        ImportSheetRow row, Dictionary<string, int> indexOf, Context context, Dictionary<(ProjectImportDimension, string), int> unmatched)
    {
        private readonly List<ProjectImportIssue> _issues = [];
        private readonly List<ProjectImportChange> _changes = [];
        private ProjectImportRowAction _action = ProjectImportRowAction.Create;
        private ProjectEntity? _existing;

        public ProjectImportRowPreview Preview { get; private set; } = null!;

        public ResolvedRow? Run(Dictionary<string, int> legacyCount, HashSet<string> skipped)
        {
            string? reference = Text(LegacyRef);
            string? title = Text(NameAr);
            string? titleEn = Text(NameEn);
            bool? strategic = YesNo(Strategic);
            if (reference is not null && legacyCount.GetValueOrDefault(reference) > 1)
            {
                Error(LegacyRef, ProjectImportIssueCodes.DuplicateInFile);
            }

            Guid? entity = Id(ProjectImportDimension.Entity, Entity);
            Guid? department = Id(ProjectImportDimension.Department, Department);
            Guid? sector = Id(ProjectImportDimension.Sector, Sector);
            Guid? portfolio = Id(ProjectImportDimension.Portfolio, Portfolio);
            Guid? initiative = Id(ProjectImportDimension.Initiative, Initiative);
            Guid? owner = Id(ProjectImportDimension.FollowUpOwner, FollowUpOwner);
            Guid? manager = Manager(department);
            ProjectLifecycleState? status = Status();
            Guid? governance = Id(ProjectImportDimension.Governance, Governance);
            Guid? priority = Id(ProjectImportDimension.Priority, Priority);
            Guid? region = Id(ProjectImportDimension.Region, Region);
            Guid? city = Id(ProjectImportDimension.City, City);
            Money? budget = Value<Money>(Budget);
            DateOnly? start = Value<DateOnly>(Start);
            DateOnly? end = Value<DateOnly>(End);
            string? description = Text(Description);
            decimal? planned = Percent(PlannedProgress);
            decimal? actual = Percent(ActualProgress);
            decimal? plannedSpend = Percent(PlannedSpend);
            decimal? actualSpend = Percent(ActualSpend);
            string? obstacle = Text(Obstacle);
            string? nextAction = Text(NextAction);
            string? actionOwner = Text(ActionOwner);
            DateOnly? actionDue = Value<DateOnly>(ActionDue);
            bool? compliant = YesNo(EntityCommits);
            FundingSource? funding = FundingOf(Text(Funding));
            string? notes = Text(Notes);

            if (initiative is { } i && portfolio is { } p && context.References.Items.GetValueOrDefault(i)?.ParentItemId != p)
            {
                Error(Initiative, ProjectImportIssueCodes.InitiativeOutsidePortfolio);
            }

            if (city is { } c && region is { } r && context.References.Items.GetValueOrDefault(c)?.ParentItemId is { } cityRegion && cityRegion != r)
            {
                Error(City, ValidationCode.NotAllowed);
            }

            if (start is { } s && end is { } e && e < s)
            {
                Error(End, ProjectImportIssueCodes.EndBeforeStart);
            }

            if (budget is { } amount && amount.Amount <= 0)
            {
                Error(Budget, ProjectImportIssueCodes.NotPositive);
            }

            if (department is { } d && _existing is null && !context.InScope(d, entity, manager))
            {
                Error(Department, ProjectImportIssueCodes.OutsideScope);
            }

            if (planned is null || actual is null)
            {
                Info(planned is null ? PlannedProgress : ActualProgress, ProjectImportIssueCodes.KeptBlank);
            }

            if (plannedSpend is null && actualSpend is null)
            {
                Info(ActualSpend, ProjectImportIssueCodes.KeptBlank);
            }

            Warnings(status, end, actual, budget, title, entity);
            MatchExisting(reference);
            if (reference is not null && skipped.Contains(reference))
            {
                _action = ProjectImportRowAction.Skip;
                Info(LegacyRef, ProjectImportIssueCodes.SkippedByImporter);
            }

            bool failed = _issues.Any(x => x.Level == ProjectImportIssueLevel.Error);
            ResolvedRow? resolved = failed ? null : new ResolvedRow(
                row.RowNumber, reference!, _action, _existing,
                new NarrativeText(title!, Language.Ar), titleEn, strategic, description is null ? null : new NarrativeText(description, Language.Ar),
                entity!.Value, department!.Value, sector!.Value, portfolio!.Value, initiative, owner, manager!.Value, status!.Value, governance!.Value,
                priority, budget!.Value, funding, start!.Value, end!.Value, region!.Value, city!.Value,
                planned, actual, plannedSpend, actualSpend,
                obstacle is null ? null : new NarrativeText(obstacle, Language.Ar), nextAction is null ? null : new NarrativeText(nextAction, Language.Ar),
                actionOwner, actionDue, compliant, notes is null ? null : new NarrativeText(notes, Language.Ar));
            if (resolved is not null && _existing is not null && _action == ProjectImportRowAction.Update)
            {
                Compare(resolved, _existing);
                if (_changes.Count > 0 && _existing.LifecycleState == ProjectLifecycleState.Closed)
                {
                    Error(LegacyRef, ProjectImportIssueCodes.ProjectClosed);
                    resolved = null;
                }
                else if (_changes.Count == 0)
                {
                    _action = ProjectImportRowAction.Skip;
                    Info(LegacyRef, ProjectImportIssueCodes.NoChange);
                    resolved = resolved with { Action = ProjectImportRowAction.Skip };
                }
            }

            Preview = new ProjectImportRowPreview(row.RowNumber, reference, title, _action, _existing?.Id, _issues, _changes);
            return resolved;
        }

        /// <summary>A row matches a project by its legacy reference, then by its Formal Project ID: then it updates that project.</summary>
        private void MatchExisting(string? reference)
        {
            if (reference is null)
            {
                return;
            }

            _existing = context.ByLegacyReference.GetValueOrDefault(reference) ?? context.ByFormalId.GetValueOrDefault(reference);
            if (_existing is null)
            {
                return;
            }

            _action = ProjectImportRowAction.Update;
            if (!context.Reaches(_existing.Id))
            {
                Error(LegacyRef, ProjectImportIssueCodes.OutsideScope);
            }

        }

        /// <summary>The fields an update writes, and the governed ones it leaves to a change request, each flagged when the row differs.</summary>
        private void Compare(ResolvedRow row, ProjectEntity p)
        {
            ProjectImportReferences refs = context.References;
            Change("strategicPortfolioItemId", refs.LabelOf(ProjectImportDimension.Portfolio, Id(p.StrategicPortfolioItemId)), refs.LabelOf(ProjectImportDimension.Portfolio, Id(row.StrategicPortfolioItemId)));
            Change("strategicInitiativeItemId", refs.LabelOf(ProjectImportDimension.Initiative, Id(p.StrategicInitiativeItemId)), refs.LabelOf(ProjectImportDimension.Initiative, Id(row.StrategicInitiativeItemId)));
            Change("titleEn", p.TitleEn, row.TitleEn);
            Change("isStrategic", p.IsStrategic?.ToString(), row.IsStrategic?.ToString());
            Change("priorityItemId", refs.LabelOf(ProjectImportDimension.Priority, Id(p.PriorityItemId)), refs.LabelOf(ProjectImportDimension.Priority, Id(row.PriorityItemId)));
            Change("fundingSource", p.FundingSource?.ToString(), row.Funding?.ToString());
            Change("followUpOwnerUserId", refs.LabelOf(ProjectImportDimension.FollowUpOwner, Id(p.FollowUpOwnerUserId)), refs.LabelOf(ProjectImportDimension.FollowUpOwner, Id(row.FollowUpOwnerUserId)));
            Change("currentObstacle", p.CurrentObstacle?.Text, row.Obstacle?.Text);
            Change("nextAction", p.NextAction?.Text, row.NextAction?.Text);
            Change("nextActionOwner", p.NextActionOwner, row.ActionOwner);
            Change("nextActionDueDate", Iso(p.NextActionDueDate), Iso(row.ActionDue));
            Change("declaredPlannedPercent", Number(p.DeclaredPlannedPercent), Number(row.PlannedPercent));
            Change("declaredActualPercent", Number(p.DeclaredActualPercent), Number(row.ActualPercent));
            Change("declaredPlannedSpendPercent", Number(p.DeclaredPlannedSpendPercent), Number(row.PlannedSpendPercent));
            Change("declaredActualSpendPercent", Number(p.DeclaredActualSpendPercent), Number(row.ActualSpendPercent));
            Change("entityUpdateCompliant", p.EntityUpdateCompliant?.ToString(), row.EntityCompliant?.ToString());

            // Registration fields of an approved project change through a change request (WF-08), never an import.
            Governed(NameAr, p.Title.Text != row.Title.Text);
            Governed(Department, p.DepartmentId != row.DepartmentId);
            Governed(Entity, p.ExternalEntityId != row.ExternalEntityId);
            Governed(Sector, p.ClassificationItemId != row.ClassificationItemId);
            Governed(Budget, p.RegistrationBudgetSar != row.Budget);
            Governed(Start, p.PlannedStartDate != row.Start);
            Governed(End, p.PlannedEndDate != row.End);
            Governed(Region, p.RegionItemId != row.RegionItemId);
            Governed(City, p.CityItemId != row.CityItemId);
            Governed(ProjectImportLayout.Status, p.LifecycleState != row.Status);
            Governed(Governance, p.GovernanceProfileItemId != row.GovernanceProfileItemId);
        }

        private void Governed(string column, bool differs)
        {
            if (differs)
            {
                Warning(column, ProjectImportIssueCodes.GovernedFieldNotUpdated);
            }
        }

        private void Change(string field, string? before, string? after)
        {
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                _changes.Add(new ProjectImportChange(field, before, after));
            }
        }

        private void Warnings(ProjectLifecycleState? status, DateOnly? end, decimal? actual, Money? budget, string? title, Guid? entity)
        {
            if (status == ProjectLifecycleState.Active && end < context.Today)
            {
                Warning(End, ProjectImportIssueCodes.ActivePastEnd);
            }

            if (status == ProjectLifecycleState.Completed && actual is < 100)
            {
                Warning(ActualProgress, ProjectImportIssueCodes.CompletedBelowFull);
            }

            if (status == ProjectLifecycleState.Completed && end > context.Today)
            {
                Warning(End, ProjectImportIssueCodes.CompletedFutureEnd);
            }

            if (status == ProjectLifecycleState.ApprovedPlanned && actual is > 0)
            {
                Warning(ActualProgress, ProjectImportIssueCodes.FutureWithProgress);
            }

            if (budget?.Amount >= ExceptionalValueSar)
            {
                Warning(Budget, ProjectImportIssueCodes.ExceptionalValue);
            }

            if (title is not null && context.ExistingNames.Contains((ImportNames.Key(title), entity))
                && !(Text(LegacyRef) is { } reference && (context.ByLegacyReference.ContainsKey(reference) || context.ByFormalId.ContainsKey(reference))))
            {
                Warning(NameAr, ProjectImportIssueCodes.SameNameAndEntity);
            }
        }

        private Guid? Manager(Guid? department)
        {
            if (Text(ProjectManager) is not { } text)
            {
                return null;
            }

            if (context.References.Resolve(ProjectImportDimension.ProjectManager, text) is not { } value)
            {
                Error(ProjectManager, ProjectImportIssueCodes.ManagerNotFound);
                unmatched[(ProjectImportDimension.ProjectManager, text)] = unmatched.GetValueOrDefault((ProjectImportDimension.ProjectManager, text)) + 1;
                return null;
            }

            Guid id = Guid.Parse(value);
            if (!context.IsProjectManager(id, department))
            {
                Error(ProjectManager, ProjectImportIssueCodes.ManagerNotR04);
            }

            return id;
        }

        private ProjectLifecycleState? Status()
        {
            string text = Text(ProjectImportLayout.Status) ?? string.Empty;
            if (context.References.Resolve(ProjectImportDimension.Status, text) is { } code && ProjectImportReferences.StateOf(code) is { } state)
            {
                return state;
            }

            // أخرى or blank: the importer chooses, by mapping the word; the row is blocked until then.
            Error(ProjectImportLayout.Status, ProjectImportIssueCodes.StatusToChoose);
            unmatched[(ProjectImportDimension.Status, text)] = unmatched.GetValueOrDefault((ProjectImportDimension.Status, text)) + 1;
            return null;
        }

        private Guid? Id(ProjectImportDimension dimension, string column)
        {
            if (Text(column) is not { } text)
            {
                return null;
            }

            if (context.References.Resolve(dimension, text) is { } value && Guid.TryParse(value, out Guid id))
            {
                return id;
            }

            // The follow-up owner is optional (review §3): a name with no account is left empty and flagged, and may still be mapped.
            if (dimension == ProjectImportDimension.FollowUpOwner)
            {
                Warning(column, ProjectImportIssueCodes.OwnerUnmatched);
            }
            else
            {
                Error(column, ProjectImportIssueCodes.NameUnmatched);
            }

            unmatched[(dimension, text)] = unmatched.GetValueOrDefault((dimension, text)) + 1;
            return null;
        }

        /// <summary>A cell's text, checked against its column's shape; null when blank or broken (the issue is recorded).</summary>
        private string? Text(string key) => Parsed(key) as string;

        private T? Value<T>(string key)
            where T : struct =>
            Parsed(key) is T value ? value : null;

        /// <summary>
        /// A percentage as RPMO's sheet holds it: a fraction (0.83, 1 for 100%) or a number of points (83, read as 83% and noted). Outside
        /// 0–100% is an error.
        /// </summary>
        private decimal? Percent(string key)
        {
            if (Value<decimal>(key) is not { } number)
            {
                return null;
            }

            decimal percent = number;
            // A percentage cell holds 100% as 1, so a value up to 1 is a fraction.
            if (number is >= 0 and <= 1)
            {
                percent = decimal.Round(number * 100, 4);
            }
            else if (number is > 1 and <= 100)
            {
                Info(key, ProjectImportIssueCodes.PercentScaled);
            }

            if (percent is < 0 or > 100)
            {
                Error(key, ProjectImportIssueCodes.PercentOutOfRange);
                return null;
            }

            return percent;
        }

        private bool? YesNo(string key) => ImportNames.Key(Text(key)) switch
        {
            "" => null,
            "نعم" or "yes" or "true" => true,
            "لا" or "no" or "false" => false,
            _ => Refuse<bool>(key),
        };

        private FundingSource? FundingOf(string? text) => ImportNames.Key(text) switch
        {
            "" => null,
            "نعم" or "yes" or "كليا" or "كامل" => FundingSource.StrategyBudgetFull,
            "جزئي" or "جزئيا" or "partial" => FundingSource.StrategyBudgetPartial,
            "لا" or "no" => FundingSource.NotStrategyBudget,
            _ => Refuse<FundingSource>(ProjectImportLayout.Funding),
        };

        private T? Refuse<T>(string key)
            where T : struct
        {
            Error(key, ValidationCode.EnumValue);
            return null;
        }

        private readonly Dictionary<string, object?> _parsed = new(StringComparer.Ordinal);

        private object? Parsed(string key)
        {
            if (_parsed.TryGetValue(key, out object? known))
            {
                return known;
            }

            object? value = ImportCellParser.Parse(ColumnOf(key), Cell(row, indexOf, key), out string? issue);
            if (issue is not null)
            {
                Error(key, issue);
            }

            _parsed[key] = value;
            return value;
        }

        private void Error(string column, string code) => _issues.Add(new ProjectImportIssue(column, code, ProjectImportIssueLevel.Error));

        private void Warning(string column, string code) => _issues.Add(new ProjectImportIssue(column, code, ProjectImportIssueLevel.Warning));

        private void Info(string column, string code) => _issues.Add(new ProjectImportIssue(column, code, ProjectImportIssueLevel.Info));

        private static string? Id(Guid? id) => id?.ToString("D");

        private static string? Iso(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static string? Number(decimal? value) => value?.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
