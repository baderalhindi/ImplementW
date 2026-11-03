using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMPlatform.Application.Features.Project.Import;
using PMPlatform.Domain.Project;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Project;

/// <summary>Project import batches and remembered name mappings (R3-4), in the <c>project</c> schema.</summary>
internal sealed class ProjectImportRepository(PMPlatformDbContext context) : IProjectImportRepository
{
    public async Task<ProjectImportBatch?> FindAsync(Guid batchId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectImportBatch? batch = await context.Set<ProjectImportBatch>().SingleOrDefaultAsync(b => b.Id == batchId, cancellationToken).ConfigureAwait(false);
        if (batch is not null)
        {
            AdministrationPersistence.ExpectVersion(context, batch, expectedVersion);
        }

        return batch;
    }

    public Task<ProjectImportBatch?> ReadAsync(Guid batchId, CancellationToken cancellationToken) =>
        Summaries(context.Set<ProjectImportBatch>().AsNoTracking().Where(b => b.Id == batchId)).SingleOrDefaultAsync(cancellationToken);

    public uint RowVersionOf(ProjectImportBatch batch) =>
        context.Entry(batch).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public async Task<(IReadOnlyList<ProjectImportBatch> Items, int TotalCount)> ListAsync(
        ProjectImportBatchStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<ProjectImportBatch> rows = context.Set<ProjectImportBatch>().AsNoTracking();
        if (status is { } only)
        {
            rows = rows.Where(b => b.Status == only);
        }

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<ProjectImportBatch> items = await Summaries(rows.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    public async Task<int> NextBatchNoAsync(CancellationToken cancellationToken) =>
        (await context.Set<ProjectImportBatch>().MaxAsync(b => (int?)b.BatchNo, cancellationToken).ConfigureAwait(false) ?? 0) + 1;

    public void Add(ProjectImportBatch batch) => context.Add(batch);

    public async Task<IReadOnlyList<ProjectImportNameMapping>> ListMappingsAsync(CancellationToken cancellationToken) =>
        await context.Set<ProjectImportNameMapping>().ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(ProjectImportNameMapping mapping) => context.Add(mapping);

    public void Add(ProjectImportValidation validation) => context.Add(validation);

    public async Task<ProjectImportValidation?> FindValidationAsync(Guid validationId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectImportValidation? validation = await context.Set<ProjectImportValidation>()
            .SingleOrDefaultAsync(v => v.Id == validationId, cancellationToken).ConfigureAwait(false);
        if (validation is not null)
        {
            AdministrationPersistence.ExpectVersion(context, validation, expectedVersion);
        }

        return validation;
    }

    public async Task<IReadOnlyList<(Guid Id, uint Version)>> ListQueuedValidationsAsync(int batchSize, CancellationToken cancellationToken) =>
        [.. (await context.Set<ProjectImportValidation>().AsNoTracking()
            .Where(v => v.Status == ProjectImportValidationStatus.Queued)
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id)
            .Select(v => new { v.Id, Version = EF.Property<uint>(v, EntityTypeBuilderExtensions.RowVersion) })
            .Take(batchSize).ToListAsync(cancellationToken).ConfigureAwait(false)).Select(v => (v.Id, v.Version))];

    public void Add(ProjectImportValueRequest request) => context.Add(request);

    public async Task<ProjectImportValueRequest?> FindValueRequestAsync(Guid requestId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectImportValueRequest? request = await context.Set<ProjectImportValueRequest>()
            .SingleOrDefaultAsync(r => r.Id == requestId, cancellationToken).ConfigureAwait(false);
        if (request is not null)
        {
            AdministrationPersistence.ExpectVersion(context, request, expectedVersion);
        }

        return request;
    }

    public Task<ProjectImportValueRequest?> FindOpenValueRequestAsync(string dimension, string sourceText, CancellationToken cancellationToken) =>
        context.Set<ProjectImportValueRequest>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Dimension == dimension && r.SourceText == sourceText && r.Status == ProjectImportValueRequestStatus.Open, cancellationToken);

    public async Task<(IReadOnlyList<ProjectImportValueRequest> Items, int TotalCount)> ListValueRequestsAsync(
        ProjectImportValueRequestStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<ProjectImportValueRequest> rows = context.Set<ProjectImportValueRequest>().AsNoTracking();
        if (status is { } only)
        {
            rows = rows.Where(r => r.Status == only);
        }

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<ProjectImportValueRequest> items = await rows.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    public uint RowVersionOf(ProjectImportValueRequest request) =>
        context.Entry(request).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public void Clear() => context.ChangeTracker.Clear();

    public async Task<IAsyncDisposable> BeginAsync(CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // One batch is applied or rolled back at a time, so two approvals never draw the same Formal Project IDs' rows against each other.
        await context.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtext('project.project_import_batch'), 0)", cancellationToken).ConfigureAwait(false);
        return new Work(context, transaction);
    }

    public Task CommitAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is { } transaction
            ? transaction.CommitAsync(cancellationToken)
            : throw new InvalidOperationException("No import batch transaction is open.");

    public async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return false;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another submission drew the same batch number.
            context.ChangeTracker.Clear();
            return false;
        }
    }

    /// <summary>A batch without its file's bytes, which only its download reads.</summary>
    private static IQueryable<ProjectImportBatch> Summaries(IQueryable<ProjectImportBatch> rows) =>
        rows.Select(b => new ProjectImportBatch
        {
            Id = b.Id,
            BatchNo = b.BatchNo,
            Status = b.Status,
            FileName = b.FileName,
            FileSha256 = b.FileSha256,
            FileContent = Array.Empty<byte>(),
            SubmissionNote = b.SubmissionNote,
            Plan = b.Plan,
            RowCount = b.RowCount,
            CreateCount = b.CreateCount,
            UpdateCount = b.UpdateCount,
            SkipCount = b.SkipCount,
            WarningCount = b.WarningCount,
            DecidedByUserId = b.DecidedByUserId,
            DecidedAt = b.DecidedAt,
            ReturnReason = b.ReturnReason,
            RolledBackByUserId = b.RolledBackByUserId,
            RolledBackAt = b.RolledBackAt,
            CreatedAt = b.CreatedAt,
            CreatedBy = b.CreatedBy,
            UpdatedAt = b.UpdatedAt,
            UpdatedBy = b.UpdatedBy,
        });

    private sealed class Work(PMPlatformDbContext context, IDbContextTransaction transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            context.ChangeTracker.Clear();
        }
    }
}
