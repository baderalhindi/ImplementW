using System.Text.RegularExpressions;

namespace PMPlatform.Tests.Unit.Architecture;

/// <summary>A-5: the data tier is written only through Infrastructure/Persistence (T-2, T-5, M-13).</summary>
/// <remarks>
/// What can be checked from compiled code: migrations live only in their folder, and no SQL write statement is
/// embedded outside Persistence. Whether a migration's SQL stays inside its module's schema is checked at
/// migration review (TASK-024), where the schema is known.
/// </remarks>
public sealed partial class PersistenceBoundaryTests
{
    private const string PersistenceNamespace = "PMPlatform.Infrastructure.Persistence";
    private const string MigrationsNamespace = "PMPlatform.Infrastructure.Persistence.Migrations";
    private const string MigrationBaseType = "Microsoft.EntityFrameworkCore.Migrations.Migration";

    [Fact]
    public void MigrationsLiveOnlyInThePersistenceMigrationsFolder()
    {
        IEnumerable<string> misplaced = Solution.AllTypes()
            .Where(t => Solution.DerivesFrom(t, MigrationBaseType))
            .Where(t => Solution.NamespaceOf(t) != MigrationsNamespace)
            .Select(t => t.FullName);

        Assert.Empty(misplaced);
    }

    [Fact]
    public void NoSqlWriteStatementExistsOutsidePersistence()
    {
        IEnumerable<string> violations = Solution.AllTypes()
            .Where(t => !Solution.NamespaceOf(t).StartsWith(PersistenceNamespace, StringComparison.Ordinal))
            .SelectMany(t => Solution.StringLiterals(t).Select(s => (Type: t, Literal: s)))
            .Where(x => SqlWriteStatement().IsMatch(x.Literal))
            .Select(x => $"{x.Type.FullName}: \"{x.Literal}\"");

        Assert.Empty(violations);
    }

    [GeneratedRegex(@"\b(INSERT\s+INTO|UPDATE\s+[\w.""]+\s+SET|DELETE\s+FROM|TRUNCATE\s+TABLE|ALTER\s+TABLE|DROP\s+TABLE|CREATE\s+TABLE)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SqlWriteStatement();
}
