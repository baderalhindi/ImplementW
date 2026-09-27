using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMPlatform.Application.Features.IdentityAccess.Administration;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>What the FG-03 administration repositories share: saving with its two expected failures, versions and text filters.</summary>
internal static class AdministrationPersistence
{
    /// <summary>The unique indexes a request can collide with, and the request field each one is.</summary>
    private static readonly Dictionary<string, string> UniqueKeyFields = new(StringComparer.Ordinal)
    {
        ["ix_user_username"] = "username",
        ["ix_user_email"] = "email",
        ["ix_user_directory_subject_id"] = "directorySubjectId",
        ["ix_department_code"] = "code",
        ["ix_external_entity_code"] = "code",
    };

    /// <summary>
    /// A stale version (R-21) and a taken unique key are answers, not faults. The unique index is the only check, so two
    /// concurrent creates cannot both pass it. After either failure nothing stays tracked.
    /// </summary>
    public static async Task<SaveResult> SaveAsync(PMPlatformDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return SaveResult.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return SaveResult.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation)
        {
            context.ChangeTracker.Clear();
            return new SaveResult(
                SaveOutcome.DuplicateKey,
                violation.ConstraintName is { } constraint && UniqueKeyFields.TryGetValue(constraint, out string? field) ? field : null);
        }
    }

    /// <summary>Makes <paramref name="expectedVersion"/> the version the update is conditional on.</summary>
    public static void ExpectVersion<TEntity>(PMPlatformDbContext context, TEntity entity, uint? expectedVersion)
        where TEntity : class
    {
        if (expectedVersion is { } version)
        {
            context.Entry(entity).Property<uint>(EntityTypeBuilderExtensions.RowVersion).OriginalValue = version;
        }
    }

    /// <summary>An ILIKE pattern matching <paramref name="text"/> anywhere, with its wildcards taken literally.</summary>
    public static string ContainsPattern(string text) =>
        $"%{text.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal).Replace("_", @"\_", StringComparison.Ordinal)}%";
}
