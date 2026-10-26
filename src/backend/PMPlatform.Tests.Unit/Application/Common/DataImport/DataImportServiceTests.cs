using Microsoft.Extensions.Logging.Abstractions;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Common.Validation;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Unit.Application.Auditing;

namespace PMPlatform.Tests.Unit.Application.Common.DataImport;

/// <summary>
/// TASK-102: a workbook loads whole or not at all; every refused cell and row is reported; a validation writes what an import would and
/// keeps none of it; a legacy ID loaded before is skipped, never loaded twice.
/// </summary>
public sealed class DataImportServiceTests
{
    private static readonly Guid Actor = Guid.Parse("00000000-0000-4000-8000-000000000001");

    private readonly FakeRegister _register = new();
    private readonly FakeWorkbook _workbook = new();
    private readonly FakeImportRepository _repository = new();
    private readonly RecordingAuditTrail _audit = new();
    private readonly StubAuthorization _authorization = new();

    [Fact]
    public async Task AHeaderThatDoesNotMatchTheRegisterIsReportedAndNothingIsWritten()
    {
        _workbook.Rows = [Row(1, "legacy_id", "nmae", "legacy_id"), Row(2, "L1", "A")];

        ImportReport report = await ImportAsync(commit: true);

        Assert.Equal(
            [new ImportIssue(1, "nmae", ImportIssueCodes.ColumnUnknown), new ImportIssue(1, "legacy_id", ImportIssueCodes.ColumnDuplicate), new ImportIssue(1, "name", ImportIssueCodes.ColumnMissing)],
            report.Issues);
        Assert.False(report.Committed);
        Assert.Empty(_register.Applied);
        Assert.False(_repository.Begun);
    }

    [Fact]
    public async Task AWorkbookWithAHeaderAndNoRowIsReportedAsEmpty()
    {
        _workbook.Rows = [Row(1, "legacy_id", "name")];

        ImportReport report = await ImportAsync(commit: true);

        Assert.Equal([new ImportIssue(2, string.Empty, ImportIssueCodes.NoRows)], report.Issues);
    }

    [Fact]
    public async Task EveryBadCellAndEveryRefusedRowIsReportedAndNothingIsCommitted()
    {
        _workbook.Rows =
        [
            Row(1, "name", "legacy_id"),
            Row(2, "Good", "L1"),
            Row(3, string.Empty, "L2"),
            Row(4, "Refused", "L3"),
            Row(5, "Again", "L1"),
            Row(6, "Fine", "L4"),
        ];
        _register.Refuse["Refused"] = new ImportCellIssue("name", "SOME_RULE");

        ImportReport report = await ImportAsync(commit: true);

        Assert.Equal(
            [
                new ImportIssue(3, "name", ValidationCode.Required),
                new ImportIssue(5, "legacy_id", ImportIssueCodes.DuplicateInFile),
                new ImportIssue(4, "name", "SOME_RULE"),
            ],
            report.Issues);
        Assert.Equal(["Good", "Refused", "Fine"], _register.Applied);
        Assert.Equal(1, _repository.Undone);
        Assert.Equal(
            [ImportRowStatus.Ready, ImportRowStatus.Rejected, ImportRowStatus.Rejected, ImportRowStatus.Rejected, ImportRowStatus.Ready],
            report.Rows.Select(r => r.Status));
        Assert.All(report.Rows, r => Assert.Null(r.EntityId));
        Assert.False(report.Committed);
        Assert.False(_repository.Committed);
        Assert.Null(_repository.Batch);
        Assert.Empty(_audit.Staged);
    }

    [Fact]
    public async Task AValidationRunsEveryRowAndKeepsNone()
    {
        _workbook.Rows = [Row(1, "legacy_id", "name"), Row(2, "L1", "A"), Row(3, "L2", "B")];

        ImportReport report = await ImportAsync(commit: false);

        Assert.Equal(["A", "B"], _register.Applied);
        Assert.Equal((false, 2, 2, 0), (report.Committed, report.RowCount, report.CreatedCount, report.SkippedCount));
        Assert.All(report.Rows, r => Assert.Equal((ImportRowStatus.Ready, false), (r.Status, r.EntityId.HasValue)));
        Assert.True(_repository.Disposed);
        Assert.False(_repository.Committed);
        Assert.Null(_repository.Batch);
    }

    [Fact]
    public async Task AnImportCommitsTheRowsTheirLineageAndOneAuditedBatch()
    {
        _workbook.Rows = [Row(1, "legacy_id", "name"), Row(2, "L1", "A"), Row(3, "L2", "B")];

        ImportReport report = await ImportAsync(commit: true, fileName: "departments.xlsx");

        Assert.True(report.Committed);
        Assert.True(_repository.Committed);
        ImportBatch batch = Assert.IsType<ImportBatch>(_repository.Batch);
        Assert.Equal(("FAKE", "departments.xlsx", 2, 2, 0, Actor), (batch.RegisterCode, batch.FileName, batch.RowCount, batch.CreatedCount, batch.SkippedCount, batch.CreatedBy));
        Assert.Equal(64, batch.FileSha256.Length);
        Assert.Equal(batch.Id, report.BatchId);
        Assert.Equal(
            [("L1", _register.IdOf("A"), 2), ("L2", _register.IdOf("B"), 3)],
            _repository.Lineage.Select(r => (r.LegacyId, r.EntityId, r.RowNumber)));
        Assert.All(_repository.Lineage, r => Assert.Equal((batch.Id, "FAKE"), (r.ImportBatchId, r.RegisterCode)));
        Assert.Equal([_register.IdOf("A"), _register.IdOf("B")], report.Rows.Select(r => r.EntityId));
        Assert.All(report.Rows, r => Assert.Equal(ImportRowStatus.Created, r.Status));
        Assert.Equal(("Fake.RecordsImported", AuditEventClass.PrivilegedAction, batch.Id), (_audit.Staged[0].EventType, _audit.Staged[0].EventClass, _audit.Staged[0].Subject!.Id));
    }

    [Fact]
    public async Task ARowLoadedByAnEarlierBatchIsSkippedNotLoadedAgain()
    {
        Guid earlier = Guid.NewGuid();
        _repository.Imported[("FAKE", "L1")] = earlier;
        _workbook.Rows = [Row(1, "legacy_id", "name"), Row(2, "L1", "A"), Row(3, "L2", "B")];

        ImportReport report = await ImportAsync(commit: true);

        Assert.Equal(["B"], _register.Applied);
        Assert.Equal((ImportRowStatus.Skipped, earlier), (report.Rows[0].Status, report.Rows[0].EntityId!.Value));
        Assert.Equal((1, 1), (report.CreatedCount, report.SkippedCount));
        Assert.Equal(1, _repository.Batch!.SkippedCount);
    }

    [Fact]
    public async Task ARowMayReferToALegacyIdLoadedEarlierInTheSameWorkbook()
    {
        _workbook.Rows = [Row(1, "legacy_id", "name", "parent"), Row(2, "L1", "A"), Row(3, "L2", "B", "L1")];

        await ImportAsync(commit: false);

        Assert.Equal(_register.IdOf("A"), _register.ParentsFound["B"]);
    }

    [Fact]
    public async Task AnImportIsRefusedBeforeAnyRowIsRead()
    {
        _workbook.Rows = [Row(1, "legacy_id", "name"), .. Enumerable.Range(2, ImportLimits.MaxRows + 1).Select(r => Row(r, $"L{r}", "A"))];
        Assert.Equal(DataImportRefusal.TooManyRows, (await Service().ImportAsync(Actor, "FAKE", Upload(), commit: true, CancellationToken.None)).Refusal);

        _workbook.Rows = null;
        Assert.Equal(DataImportRefusal.NotWorkbook, (await Service().ImportAsync(Actor, "FAKE", Upload(), commit: true, CancellationToken.None)).Refusal);
        Assert.Equal(DataImportRefusal.NotFound, (await Service().ImportAsync(Actor, "NONE", Upload(), commit: true, CancellationToken.None)).Refusal);

        _authorization.Allow = false;
        Assert.Equal(DataImportRefusal.Forbidden, (await Service().ImportAsync(Actor, "FAKE", Upload(), commit: true, CancellationToken.None)).Refusal);
        Assert.Equal(DataImportRefusal.Forbidden, (await Service().TemplateAsync(Actor, "FAKE", CancellationToken.None)).Refusal);
        Assert.Equal(PermissionCatalogue.DataImport, _authorization.Asked);
        Assert.Empty(_register.Applied);
    }

    [Fact]
    public async Task TheTemplateHasTheLegacyIdFirstThenTheRegistersColumns()
    {
        DataImportResult<byte[]> template = await Service().TemplateAsync(Actor, "FAKE", CancellationToken.None);

        Assert.NotNull(template.Value);
        Assert.Equal(["legacy_id", "name", "parent"], _workbook.TemplateColumns!.Select(c => c.Key));
    }

    private async Task<ImportReport> ImportAsync(bool commit, string fileName = "book.xlsx") =>
        (await Service().ImportAsync(Actor, "FAKE", Upload(fileName), commit, CancellationToken.None)).Value!;

    private DataImportService Service() =>
        new([_register], _workbook, _repository, _authorization, _audit, TimeProvider.System, NullLogger<DataImportService>.Instance);

    private static ImportUpload Upload(string fileName = "book.xlsx") => new(fileName, new MemoryStream([1, 2, 3]));

    private static ImportSheetRow Row(int number, params string[] cells) =>
        new(number, cells.Select((c, i) => (c, i)).Where(x => x.c.Length > 0).ToDictionary(x => x.i, x => x.c));

    private sealed class FakeRegister : IImportRegister
    {
        private readonly Dictionary<string, Guid> _ids = [];

        public List<string> Applied { get; } = [];

        public Dictionary<string, ImportCellIssue> Refuse { get; } = [];

        public Dictionary<string, Guid?> ParentsFound { get; } = [];

        public string Code => "FAKE";

        public string OwningModule => "Fake";

        public IReadOnlyList<ImportColumn> Columns { get; } =
        [
            new("name", ImportValueKind.Text, Required: true, "Name", "الاسم"),
            new("parent", ImportValueKind.Text, Required: false, "Parent", "الأصل"),
        ];

        public Guid IdOf(string name) => _ids.TryGetValue(name, out Guid id) ? id : _ids[name] = Guid.NewGuid();

        public Task<IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>>> ReferenceValuesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>>>(new Dictionary<string, IReadOnlyList<ImportReferenceValue>>());

        public async Task<ImportRowResult> ApplyAsync(ImportRecord record, ImportContext context, CancellationToken cancellationToken)
        {
            string name = record.Text("name")!;
            Applied.Add(name);
            if (record.Text("parent") is { } parent)
            {
                ParentsFound[name] = await context.FindImportedAsync(Code, parent, cancellationToken);
            }

            return Refuse.TryGetValue(name, out ImportCellIssue? issue) ? ImportRowResult.Rejected(issue.Column, issue.Code) : ImportRowResult.Created(IdOf(name));
        }
    }

    private sealed class FakeWorkbook : IImportWorkbook
    {
        public List<ImportSheetRow>? Rows { get; set; } = [];

        public IReadOnlyList<ImportColumn>? TemplateColumns { get; private set; }

        public ImportSheet? Read(Stream content, int rowLimit) => Rows is null ? null : new ImportSheet([.. Rows.Take(rowLimit)]);

        public byte[] WriteTemplate(IReadOnlyList<ImportColumn> columns, IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>> referenceValues)
        {
            TemplateColumns = columns;
            return [0x50, 0x4b];
        }
    }

    private sealed class FakeImportRepository : IImportRepository
    {
        public Dictionary<(string, string), Guid> Imported { get; } = [];

        public bool Begun { get; private set; }

        public int Undone { get; set; }

        public bool Committed { get; set; }

        public bool Disposed { get; set; }

        public ImportBatch? Batch { get; private set; }

        public IReadOnlyList<ImportedRecord> Lineage { get; private set; } = [];

        public Task<IImportWork> BeginAsync(string registerCode, CancellationToken cancellationToken)
        {
            Begun = true;
            return Task.FromResult<IImportWork>(new FakeWork(this));
        }

        public Task<Guid?> FindImportedAsync(string registerCode, string legacyId, CancellationToken cancellationToken) =>
            Task.FromResult(Imported.TryGetValue((registerCode, legacyId), out Guid id) ? id : (Guid?)null);

        public Task SaveBatchAsync(ImportBatch batch, IReadOnlyList<ImportedRecord> records, CancellationToken cancellationToken)
        {
            Batch = batch;
            Lineage = records;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWork(FakeImportRepository repository) : IImportWork
    {
        public Task MarkRowAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UndoRowAsync(CancellationToken cancellationToken)
        {
            repository.Undone++;
            return Task.CompletedTask;
        }

        public Task EndRowAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            repository.Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            repository.Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubAuthorization : IAuthorizationEngine
    {
        public bool Allow { get; set; } = true;

        public string? Asked { get; private set; }

        public Task<AuthorizationDecision> AuthorizeAsync(Guid userId, AuthorizationRequest request, CancellationToken cancellationToken)
        {
            Asked = request.PermissionCode;
            return Task.FromResult(Allow ? AuthorizationDecision.Allowed : AuthorizationDecision.Forbidden(AuthorizationDenial.None));
        }

        public Task<AuthorizationPrincipal?> GetPrincipalAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AuthorizationDecision> EvaluateAsync(Guid userId, AuthorizationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RecordScope> GetRecordScopeAsync(Guid userId, string permissionCode, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<FieldMask> GetFieldMaskAsync(Guid userId, string permissionCode, string entityCode, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
