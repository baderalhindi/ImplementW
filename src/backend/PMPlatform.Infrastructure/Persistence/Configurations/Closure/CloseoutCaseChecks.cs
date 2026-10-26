using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Closure;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Closure;

/// <summary>The state facts a completion case and a closure case share, as CHECK constraints (TASK-063).</summary>
internal static class CloseoutCaseChecks
{
    /// <summary>The statuses that are not final: at most one such case of each kind per project (CLO-CC-05, CLO-CC-06).</summary>
    public const string OpenFilter = "status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'APPROVED')";

    public static void Apply<TCase>(EntityTypeBuilder<TCase> builder)
        where TCase : CloseoutCase
    {
        builder.HasCheck("revision_no", "revision_no >= 1");

        // A DRAFT is a case never submitted; a case withdrawn may have been or not; every other has been submitted.
        builder.HasCheck("submitted", "status = 'WITHDRAWN' OR (status = 'DRAFT') = (submitted_at IS NULL)");

        // An EFFECTED case says when it took effect, and only it.
        builder.HasCheck("effected", "(status = 'EFFECTED') = (effected_at IS NOT NULL)");
    }
}
