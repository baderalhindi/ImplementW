using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Common.DataImport;

/// <summary>A workbook as uploaded.</summary>
public sealed record ImportUpload(string FileName, Stream Content);

/// <summary>Why an import or a template was refused before any row was read.</summary>
public enum DataImportRefusal
{
    None = 0,
    NotFound = 1,
    Forbidden = 2,

    /// <summary>The file is not an XLSX workbook the engine can read.</summary>
    NotWorkbook = 3,

    /// <summary>More rows than <see cref="ImportLimits.MaxRows"/>: split the workbook.</summary>
    TooManyRows = 4,
}

public sealed record DataImportResult<T>(T? Value, DataImportRefusal Refusal)
    where T : class
{
    public static implicit operator DataImportResult<T>(T value) => new(value, DataImportRefusal.None);

    public static implicit operator DataImportResult<T>(DataImportRefusal refusal) => new(null, refusal);
}

/// <summary>The template of each register and the validation or import of a workbook (TASK-102).</summary>
public interface IDataImportService
{
    public Task<DataImportResult<byte[]>> TemplateAsync(Guid actorId, string registerCode, CancellationToken cancellationToken);

    /// <summary>
    /// Writes every row in one transaction. With <paramref name="commit"/> false, or when any row is refused, the transaction is rolled
    /// back: a validation runs exactly the writes an import would, so it finds what the database itself would refuse.
    /// </summary>
    public Task<DataImportResult<ImportReport>> ImportAsync(Guid actorId, string registerCode, ImportUpload upload, bool commit, CancellationToken cancellationToken);
}

/// <summary>
/// The migration import (TASK-102; legacy-data-migration.md §3): the workbook's shape here, each row's meaning in the register's module.
/// All or nothing (Guide §9.5 steps 6–7): a workbook loads whole or not at all, and every refused row is reported, so the cutover's
/// counts reconcile. A row whose legacy ID was loaded before is skipped, so a workbook can be loaded again (BE-027).
/// </summary>
internal sealed partial class DataImportService(
    IEnumerable<IImportRegister> registers,
    IImportWorkbook workbook,
    IImportRepository repository,
    IAuthorizationEngine authorization,
    IAuditTrail audit,
    TimeProvider timeProvider,
    ILogger<DataImportService> logger) : IDataImportService
{
    private const int HeaderRow = 1;

    public async Task<DataImportResult<byte[]>> TemplateAsync(Guid actorId, string registerCode, CancellationToken cancellationToken)
    {
        if (await RegisterAsync(actorId, registerCode, cancellationToken).ConfigureAwait(false) is not { } register)
        {
            return registers.Any(r => r.Code == registerCode) ? DataImportRefusal.Forbidden : DataImportRefusal.NotFound;
        }

        IReadOnlyDictionary<string, IReadOnlyList<ImportReferenceValue>> references = await register.ReferenceValuesAsync(cancellationToken).ConfigureAwait(false);
        return workbook.WriteTemplate(ColumnsOf(register), references);
    }

    public async Task<DataImportResult<ImportReport>> ImportAsync(
        Guid actorId, string registerCode, ImportUpload upload, bool commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        if (await RegisterAsync(actorId, registerCode, cancellationToken).ConfigureAwait(false) is not { } register)
        {
            return registers.Any(r => r.Code == registerCode) ? DataImportRefusal.Forbidden : DataImportRefusal.NotFound;
        }

        using MemoryStream buffer = new();
        await upload.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)));
        buffer.Position = 0;
        if (workbook.Read(buffer, rowLimit: ImportLimits.MaxRows + 2) is not { } sheet)
        {
            return DataImportRefusal.NotWorkbook;
        }

        if (sheet.Rows.Count - 1 > ImportLimits.MaxRows)
        {
            return DataImportRefusal.TooManyRows;
        }

        IReadOnlyList<ImportColumn> columns = ColumnsOf(register);
        (Dictionary<int, ImportColumn> layout, List<ImportIssue> issues) = Layout(sheet.Rows.Count > 0 ? sheet.Rows[0] : null, columns);
        List<ImportSheetRow> rows = [.. sheet.Rows.Skip(1)];
        if (issues.Count == 0 && rows.Count == 0)
        {
            issues.Add(new ImportIssue(HeaderRow + 1, string.Empty, ImportIssueCodes.NoRows));
        }

        return issues.Count > 0
            ? new ImportReport(register.Code, Committed: false, BatchId: null, rows.Count, 0, 0, issues, [])
            : await LoadAsync(actorId, register, upload.FileName, sha256, ParseRows(rows, layout, columns, issues), issues, commit, cancellationToken)
                .ConfigureAwait(false);
    }

    private static IReadOnlyList<ImportColumn> ColumnsOf(IImportRegister register) => [ImportColumn.LegacyId, .. register.Columns];

    /// <summary>Which column each header names. Every required header must be present; a header the register does not have is refused.</summary>
    private static (Dictionary<int, ImportColumn> Layout, List<ImportIssue> Issues) Layout(ImportSheetRow? header, IReadOnlyList<ImportColumn> columns)
    {
        Dictionary<int, ImportColumn> layout = [];
        List<ImportIssue> issues = [];
        foreach ((int index, string text) in header?.Cells.OrderBy(c => c.Key) ?? Enumerable.Empty<KeyValuePair<int, string>>())
        {
            string key = text.Trim();
            if (columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)) is not { } column)
            {
                issues.Add(new ImportIssue(HeaderRow, key, ImportIssueCodes.ColumnUnknown));
            }
            else if (layout.ContainsValue(column))
            {
                issues.Add(new ImportIssue(HeaderRow, column.Key, ImportIssueCodes.ColumnDuplicate));
            }
            else
            {
                layout[index] = column;
            }
        }

        issues.AddRange(columns.Where(c => c.Required && !layout.ContainsValue(c)).Select(c => new ImportIssue(HeaderRow, c.Key, ImportIssueCodes.ColumnMissing)));
        return (layout, issues);
    }

    /// <summary>Each row typed, or null when a cell breaks its column's shape or the legacy ID repeats an earlier row's.</summary>
    private static List<(ImportSheetRow Row, ImportRecord? Record)> ParseRows(
        List<ImportSheetRow> rows, Dictionary<int, ImportColumn> layout, IReadOnlyList<ImportColumn> columns, List<ImportIssue> issues)
    {
        HashSet<string> legacyIds = new(StringComparer.Ordinal);
        List<(ImportSheetRow, ImportRecord?)> parsed = [];
        foreach (ImportSheetRow row in rows)
        {
            Dictionary<string, object> values = new(StringComparer.Ordinal);
            int before = issues.Count;
            foreach (ImportColumn column in columns)
            {
                int? index = layout.Where(l => l.Value == column).Select(l => (int?)l.Key).FirstOrDefault();
                string? cell = index is { } i ? row.Cells.GetValueOrDefault(i) : null;
                if (ImportCellParser.Parse(column, cell, out string? issue) is { } value)
                {
                    values[column.Key] = value;
                }
                else if (issue is not null)
                {
                    issues.Add(new ImportIssue(row.RowNumber, column.Key, issue));
                }
            }

            if (values.GetValueOrDefault(ImportColumn.LegacyIdKey) is string legacyId && !legacyIds.Add(legacyId))
            {
                issues.Add(new ImportIssue(row.RowNumber, ImportColumn.LegacyIdKey, ImportIssueCodes.DuplicateInFile));
            }

            parsed.Add((row, issues.Count == before ? new ImportRecord(row.RowNumber, (string)values[ImportColumn.LegacyIdKey], values) : null));
        }

        return parsed;
    }

    private async Task<ImportReport> LoadAsync(
        Guid actorId,
        IImportRegister register,
        string fileName,
        string sha256,
        List<(ImportSheetRow Row, ImportRecord? Record)> rows,
        List<ImportIssue> issues,
        bool commit,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid batchId = Guid.CreateVersion7(now);
        Dictionary<(string Register, string LegacyId), Guid> loaded = [];
        ImportContext context = new(actorId, async (code, legacyId, ct) =>
            loaded.TryGetValue((code, legacyId), out Guid id) ? id : await repository.FindImportedAsync(code, legacyId, ct).ConfigureAwait(false));

        List<ImportRowOutcome> outcomes = [];
        List<ImportedRecord> lineage = [];
        int skipped = 0;
        await using IImportWork work = await repository.BeginAsync(register.Code, cancellationToken).ConfigureAwait(false);
        foreach ((ImportSheetRow row, ImportRecord? record) in rows)
        {
            if (record is null)
            {
                outcomes.Add(new ImportRowOutcome(row.RowNumber, string.Empty, ImportRowStatus.Rejected, null));
                continue;
            }

            if (await repository.FindImportedAsync(register.Code, record.LegacyId, cancellationToken).ConfigureAwait(false) is { } existing)
            {
                skipped++;
                outcomes.Add(new ImportRowOutcome(row.RowNumber, record.LegacyId, ImportRowStatus.Skipped, existing));
                continue;
            }

            await work.MarkRowAsync(cancellationToken).ConfigureAwait(false);
            ImportRowResult result = await register.ApplyAsync(record, context, cancellationToken).ConfigureAwait(false);
            if (result.EntityId is not { } entityId)
            {
                await work.UndoRowAsync(cancellationToken).ConfigureAwait(false);
                issues.AddRange(result.Issues.Select(i => new ImportIssue(row.RowNumber, i.Column, i.Code)));
                outcomes.Add(new ImportRowOutcome(row.RowNumber, record.LegacyId, ImportRowStatus.Rejected, null));
                continue;
            }

            await work.EndRowAsync(cancellationToken).ConfigureAwait(false);
            loaded[(register.Code, record.LegacyId)] = entityId;
            lineage.Add(new ImportedRecord
            {
                Id = Guid.CreateVersion7(now),
                ImportBatchId = batchId,
                RegisterCode = register.Code,
                LegacyId = record.LegacyId,
                EntityId = entityId,
                RowNumber = row.RowNumber,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
            outcomes.Add(new ImportRowOutcome(row.RowNumber, record.LegacyId, ImportRowStatus.Ready, entityId));
        }

        if (!commit || issues.Count > 0)
        {
            // Rolled back with the work: no row exists, so none has an id to report.
            return new ImportReport(register.Code, Committed: false, BatchId: null, rows.Count, lineage.Count, skipped, issues,
                [.. outcomes.Select(o => o.Status == ImportRowStatus.Ready ? o with { EntityId = null } : o)]);
        }

        ImportBatch batch = new()
        {
            Id = batchId,
            RegisterCode = register.Code,
            FileName = fileName,
            FileSha256 = sha256,
            RowCount = rows.Count,
            CreatedCount = lineage.Count,
            SkippedCount = skipped,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        audit.Stage(new AuditEntry(AuditEventClass.PrivilegedAction, $"{register.OwningModule}.RecordsImported", AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(register.OwningModule, nameof(ImportBatch), batch.Id),
            Attributes =
            [
                AuditAttribute.Of("register_code", batch.RegisterCode),
                AuditAttribute.Of("file_sha256", batch.FileSha256),
                AuditAttribute.Of("row_count", batch.RowCount),
                AuditAttribute.Of("created_count", batch.CreatedCount),
                AuditAttribute.Of("skipped_count", batch.SkippedCount),
            ],
        });
        await repository.SaveBatchAsync(batch, lineage, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        LogImported(logger, actorId, register.Code, batch.Id, batch.CreatedCount, batch.SkippedCount);
        return new ImportReport(register.Code, Committed: true, batch.Id, rows.Count, lineage.Count, skipped, issues,
            [.. outcomes.Select(o => o.Status == ImportRowStatus.Ready ? o with { Status = ImportRowStatus.Created } : o)]);
    }

    /// <summary>The register, when it exists and the actor may import (DATA_IMPORT is platform-wide: there is no record to scope).</summary>
    private async Task<IImportRegister?> RegisterAsync(Guid actorId, string registerCode, CancellationToken cancellationToken) =>
        registers.FirstOrDefault(r => r.Code == registerCode) is { } register
        && (await authorization.AuthorizeAsync(actorId, new AuthorizationRequest(PermissionCatalogue.DataImport, new AuthorizationSubject()), cancellationToken)
            .ConfigureAwait(false)).IsAllowed
            ? register
            : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "Data import: {ActorId} imported register {RegisterCode} as batch {BatchId}: {Created} created, {Skipped} skipped.")]
    private static partial void LogImported(ILogger logger, Guid actorId, string registerCode, Guid batchId, int created, int skipped);
}
