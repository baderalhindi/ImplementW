using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMPlatform.Application.Features.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.MasterDataConfig;

/// <summary>
/// Saving for the FG-04 repositories. Conditional updates and text filters are <see cref="IdentityAccess.AdministrationPersistence"/>'s,
/// which are not specific to FG-03.
/// </summary>
internal static class MasterDataConfigPersistence
{
    /// <summary>The unique indexes a request can collide with, and the request field each one is.</summary>
    private static readonly Dictionary<string, string> UniqueKeyFields = new(StringComparer.Ordinal)
    {
        ["ix_master_data_item_catalogue_id_code"] = "code",
        ["ix_kpi_definition_code"] = "code",
        ["ix_configuration_version_configuration_family_id_version_no"] = "familyId",
    };

    /// <summary>Raised by the <c>guard_configuration_version</c> trigger when another publication of the family committed first.</summary>
    private const string EffectiveFromOrder = "ck_configuration_version_effective_from_order";

    /// <summary>
    /// A stale version (R-21), a taken unique key and a publication overtaken by another are answers, not faults. After
    /// any of them nothing stays tracked.
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
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation, ConstraintName: EffectiveFromOrder })
        {
            context.ChangeTracker.Clear();
            return SaveResult.EffectiveFromOutOfOrder;
        }
    }
}
