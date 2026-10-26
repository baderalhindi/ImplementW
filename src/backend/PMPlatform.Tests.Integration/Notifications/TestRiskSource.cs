using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Common.Events;
using PMPlatform.Infrastructure.Persistence;

namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>
/// The Risk module, played by the tests: its risks live in <c>test_source.risk</c>, and it answers Notifications'
/// revalidation of a reminder with a risk's status as it is now (<see cref="INotificationConditionSource"/>).
/// </summary>
internal sealed class TestRiskSource(PMPlatformDbContext context) : INotificationConditionSource
{
    public string SourceModule => NotificationTestHost.RiskModule;

    public async Task<string?> FindStatusAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken) =>
        subjectType != "Risk"
            ? null
            : (await context.Database
                .SqlQuery<string>($"SELECT status AS \"Value\" FROM test_source.risk WHERE id = {subjectId}")
                .ToListAsync(cancellationToken)).SingleOrDefault();
}
