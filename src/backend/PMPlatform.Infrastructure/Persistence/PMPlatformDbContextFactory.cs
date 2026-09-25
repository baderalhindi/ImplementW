using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>
/// How <c>dotnet ef</c> builds the context (TASK-024). It bypasses PMPlatform.Api's host, so adding, listing or
/// scripting a migration needs neither the secret store nor a database. Only <c>dotnet ef database update</c>
/// connects, to the database named by the <c>DB_CONNECTION_STRING</c> environment variable.
/// </summary>
public sealed class PMPlatformDbContextFactory : IDesignTimeDbContextFactory<PMPlatformDbContext>
{
    public PMPlatformDbContext CreateDbContext(string[] args)
    {
        string? connectionString = Environment.GetEnvironmentVariable(ApplicationSecrets.DatabaseConnectionString);

        DbContextOptionsBuilder<PMPlatformDbContext> options = new();
        options.UsePlatformDatabase(connectionString);

        return new PMPlatformDbContext(options.Options);
    }
}
