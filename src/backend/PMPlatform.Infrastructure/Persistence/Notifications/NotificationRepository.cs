using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Notifications;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;
using PMPlatform.Infrastructure.Persistence.Configurations.Notifications;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Notifications;

/// <summary>The <c>notifications</c> schema (TASK-039).</summary>
internal sealed class NotificationRepository(PMPlatformDbContext context) : INotificationRepository, IAsyncDisposable
{
    private IDbContextTransaction? _claim;

    public Task<bool> IntentExistsAsync(string sourceEventType, string sourceReference, CancellationToken cancellationToken) =>
        context.Set<NotificationIntent>().AnyAsync(i => i.SourceEventType == sourceEventType && i.SourceReference == sourceReference, cancellationToken);

    /// <summary>
    /// Reads the committed envelope by its unique (kind, message key), never by an attribute of the payload (ERD D-17).
    /// The intake refused any intent whose message key was not its event type and source reference.
    /// </summary>
    public async Task<NotificationIntentEnvelope?> FindEnvelopeAsync(NotificationIntent intent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        string messageKey = EventMessageKey.Of(intent.SourceEventType, intent.SourceReference);
        string? payload = await context.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.MessageType == EventKind.NotificationIntent && m.MessageKey == messageKey)
            .Select(m => m.Payload)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return payload is null ? null : EventSerialization.Deserialize<NotificationIntentEnvelope>(payload);
    }

    public async Task<IReadOnlyList<Guid>> FindDueIntentIdsAsync(DateTimeOffset now, int count, CancellationToken cancellationToken) =>
        await context.Set<NotificationIntent>().AsNoTracking()
            .Where(i => i.Status == NotificationIntentStatus.Received || (i.Status == NotificationIntentStatus.Scheduled && i.ScheduledFor <= now))
            .OrderBy(i => i.UpdatedAt).ThenBy(i => i.Id)
            .Select(i => i.Id)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<NotificationIntent?> ClaimIntentAsync(Guid intentId, CancellationToken cancellationToken)
    {
        await BeginClaimAsync(cancellationToken).ConfigureAwait(false);
        return (await context.Set<NotificationIntent>()
                .FromSql($"""
                    SELECT *, xmin FROM notifications.notification_intent
                    WHERE id = {intentId} AND status IN ('RECEIVED', 'SCHEDULED')
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();
    }

    public Task TouchIntentAsync(Guid intentId, DateTimeOffset now, CancellationToken cancellationToken) =>
        context.Set<NotificationIntent>()
            .Where(i => i.Id == intentId && (i.Status == NotificationIntentStatus.Received || i.Status == NotificationIntentStatus.Scheduled))
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.UpdatedAt, now).SetProperty(i => i.UpdatedBy, NotificationServicePrincipal.Id), cancellationToken);

    public Task<NotificationIntent?> FindIntentAsync(Guid intentId, CancellationToken cancellationToken) =>
        context.Set<NotificationIntent>().SingleOrDefaultAsync(i => i.Id == intentId, cancellationToken);

    public async Task<IReadOnlyList<NotificationIntentParameter>> GetParametersAsync(Guid intentId, CancellationToken cancellationToken) =>
        await context.Set<NotificationIntentParameter>().AsNoTracking()
            .Where(p => p.NotificationIntentId == intentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Two publications at once can leave two PUBLISHED versions until the next retires both; the higher number is the one in force.</summary>
    public async Task<IReadOnlyDictionary<NotificationChannel, NotificationTemplate>> FindPublishedTemplatesAsync(string eventType, CancellationToken cancellationToken) =>
        (await context.Set<NotificationTemplate>().AsNoTracking()
            .Where(t => t.EventType == eventType && t.LifecycleState == GovernedLifecycleState.Published)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
        .GroupBy(t => t.Channel)
        .ToDictionary(g => g.Key, g => g.MaxBy(t => t.VersionNo)!);

    public async Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(IReadOnlyCollection<Guid> userIds, string eventFamilyCode, CancellationToken cancellationToken)
    {
        List<Guid> ids = [.. userIds];
        return await context.Set<NotificationPreference>().AsNoTracking()
            .Where(p => ids.Contains(p.UserId) && p.EventFamilyCode == eventFamilyCode)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> FindDueDeliveryIdsAsync(DateTimeOffset now, int count, CancellationToken cancellationToken) =>
        await context.Set<NotificationDelivery>().AsNoTracking()
            .Where(d => (d.Status == NotificationDeliveryStatus.Pending || d.Status == NotificationDeliveryStatus.Failed) && d.NextAttemptAt <= now)
            .OrderBy(d => d.NextAttemptAt).ThenBy(d => d.Id)
            .Select(d => d.Id)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<NotificationDelivery?> ClaimDeliveryAsync(Guid deliveryId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await BeginClaimAsync(cancellationToken).ConfigureAwait(false);
        return (await context.Set<NotificationDelivery>()
                .FromSql($"""
                    SELECT *, xmin FROM notifications.notification_delivery
                    WHERE id = {deliveryId} AND status IN ('PENDING', 'FAILED') AND next_attempt_at <= {now}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();
    }

    public Task<int> CompleteRoutedIntentsAsync(DateTimeOffset now, int count, CancellationToken cancellationToken)
    {
        IQueryable<Guid> done = context.Set<NotificationIntent>()
            .Where(i => i.Status == NotificationIntentStatus.Routed
                        && !context.Set<NotificationDelivery>().Any(d => d.NotificationIntentId == i.Id
                                                                         && (d.Status == NotificationDeliveryStatus.Pending || d.Status == NotificationDeliveryStatus.Failed)))
            .OrderBy(i => i.Id)
            .Select(i => i.Id)
            .Take(count);
        return context.Set<NotificationIntent>()
            .Where(i => done.Contains(i.Id))
            .ExecuteUpdateAsync(
                set => set.SetProperty(i => i.Status, NotificationIntentStatus.Completed)
                    .SetProperty(i => i.UpdatedAt, now)
                    .SetProperty(i => i.UpdatedBy, NotificationServicePrincipal.Id),
                cancellationToken);
    }

    public async Task<(IReadOnlyList<(NotificationDelivery Delivery, NotificationIntent Intent)> Items, int TotalCount)> ListInboxAsync(
        Guid userId, NotificationInboxQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<InboxRow> rows = Inbox(userId);
        if (query.Unread is { } unread)
        {
            rows = rows.Where(r => r.Delivery.Status == NotificationDeliveryStatus.Sent == unread);
        }

        if (query.EventFamilyCode is { } family)
        {
            rows = rows.Where(r => r.Intent.EventFamilyCode == family);
        }

        return await PageAsync(rows, query.Page.Skip, query.Page.PageSize, cancellationToken).ConfigureAwait(false);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Set<NotificationDelivery>()
            .CountAsync(d => d.RecipientUserId == userId && d.Channel == NotificationChannel.InApp && d.Status == NotificationDeliveryStatus.Sent, cancellationToken);

    public async Task<(NotificationDelivery Delivery, NotificationIntent Intent)?> FindInboxItemAsync(Guid userId, Guid deliveryId, CancellationToken cancellationToken)
    {
        InboxRow? row = await (
                from d in context.Set<NotificationDelivery>()
                join i in context.Set<NotificationIntent>() on d.NotificationIntentId equals i.Id
                where d.Id == deliveryId && d.RecipientUserId == userId && d.Channel == NotificationChannel.InApp
                      && (d.Status == NotificationDeliveryStatus.Sent || d.Status == NotificationDeliveryStatus.Read)
                select new InboxRow { Delivery = d, Intent = i })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : (row.Delivery, row.Intent);
    }

    public Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        context.Set<NotificationDelivery>()
            .Where(d => d.RecipientUserId == userId && d.Channel == NotificationChannel.InApp && d.Status == NotificationDeliveryStatus.Sent)
            .ExecuteUpdateAsync(
                set => set.SetProperty(d => d.Status, NotificationDeliveryStatus.Read)
                    .SetProperty(d => d.ReadAt, now)
                    .SetProperty(d => d.UpdatedAt, now)
                    .SetProperty(d => d.UpdatedBy, userId),
                cancellationToken);

    public async Task<(IReadOnlyList<(NotificationDelivery Delivery, NotificationIntent Intent)> Items, int TotalCount)> ListHistoryAsync(
        Guid userId, NotificationHistoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<InboxRow> rows =
            from d in context.Set<NotificationDelivery>().AsNoTracking()
            join i in context.Set<NotificationIntent>() on d.NotificationIntentId equals i.Id
            where d.RecipientUserId == userId
            select new InboxRow { Delivery = d, Intent = i };
        if (query.Channels.Count > 0)
        {
            List<NotificationChannel> channels = [.. query.Channels];
            rows = rows.Where(r => channels.Contains(r.Delivery.Channel));
        }

        if (query.Statuses.Count > 0)
        {
            List<NotificationDeliveryStatus> statuses = [.. query.Statuses];
            rows = rows.Where(r => statuses.Contains(r.Delivery.Status));
        }

        return await PageAsync(rows, query.Page.Skip, query.Page.PageSize, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.Set<NotificationPreference>().Where(p => p.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<(IReadOnlyList<NotificationTemplate> Items, int TotalCount)> ListTemplatesAsync(NotificationTemplateQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<NotificationTemplate> rows = context.Set<NotificationTemplate>().AsNoTracking();
        if (query.EventType is { } eventType)
        {
            rows = rows.Where(t => t.EventType == eventType);
        }

        if (query.EventFamilyCode is { } family)
        {
            rows = rows.Where(t => t.EventFamilyCode == family);
        }

        if (query.Channels.Count > 0)
        {
            List<NotificationChannel> channels = [.. query.Channels];
            rows = rows.Where(t => channels.Contains(t.Channel));
        }

        if (query.LifecycleStates.Count > 0)
        {
            List<GovernedLifecycleState> states = [.. query.LifecycleStates];
            rows = rows.Where(t => states.Contains(t.LifecycleState));
        }

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<NotificationTemplate> items = await rows
            .OrderBy(t => t.EventType).ThenBy(t => t.Channel).ThenByDescending(t => t.VersionNo)
            .Skip(query.Page.Skip).Take(query.Page.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return (items, total);
    }

    public async Task<NotificationTemplate?> FindTemplateAsync(Guid templateId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        NotificationTemplate? template = await context.Set<NotificationTemplate>().SingleOrDefaultAsync(t => t.Id == templateId, cancellationToken).ConfigureAwait(false);
        if (template is not null)
        {
            AdministrationPersistence.ExpectVersion(context, template, expectedVersion);
        }

        return template;
    }

    public uint RowVersionOf(NotificationTemplate notificationTemplate) =>
        context.Entry(notificationTemplate).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public async Task<int> GetLatestTemplateVersionNoAsync(string eventType, NotificationChannel channel, CancellationToken cancellationToken) =>
        await context.Set<NotificationTemplate>()
            .Where(t => t.EventType == eventType && t.Channel == channel)
            .MaxAsync(t => (int?)t.VersionNo, cancellationToken)
            .ConfigureAwait(false) ?? 0;

    public async Task<IReadOnlyList<NotificationTemplate>> GetPublishedTemplatesAsync(string eventType, NotificationChannel channel, Guid exceptTemplateId, CancellationToken cancellationToken) =>
        await context.Set<NotificationTemplate>()
            .Where(t => t.EventType == eventType && t.Channel == channel && t.LifecycleState == GovernedLifecycleState.Published && t.Id != exceptTemplateId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<NotificationIntent> Items, int TotalCount)> ListIntentsAsync(NotificationIntentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<NotificationIntent> rows = context.Set<NotificationIntent>().AsNoTracking();
        if (query.Statuses.Count > 0)
        {
            List<NotificationIntentStatus> statuses = [.. query.Statuses];
            rows = rows.Where(i => statuses.Contains(i.Status));
        }

        if (query.EventFamilyCode is { } family)
        {
            rows = rows.Where(i => i.EventFamilyCode == family);
        }

        if (query.SourceModule is { } module)
        {
            rows = rows.Where(i => i.SourceModule == module);
        }

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<NotificationIntent> items = await rows
            .OrderByDescending(i => i.ReceivedAt).ThenByDescending(i => i.Id)
            .Skip(query.Page.Skip).Take(query.Page.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return (items, total);
    }

    public async Task<IReadOnlyList<NotificationDelivery>> GetDeliveriesAsync(Guid intentId, CancellationToken cancellationToken) =>
        await context.Set<NotificationDelivery>().AsNoTracking()
            .Where(d => d.NotificationIntentId == intentId)
            .OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<NotificationDelivery> Items, int TotalCount)> ListDeliveriesAsync(NotificationDeliveryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<NotificationDelivery> rows = context.Set<NotificationDelivery>().AsNoTracking();
        if (query.Statuses.Count > 0)
        {
            List<NotificationDeliveryStatus> statuses = [.. query.Statuses];
            rows = rows.Where(d => statuses.Contains(d.Status));
        }

        if (query.Channels.Count > 0)
        {
            List<NotificationChannel> channels = [.. query.Channels];
            rows = rows.Where(d => channels.Contains(d.Channel));
        }

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<NotificationDelivery> items = await rows
            .OrderByDescending(d => d.UpdatedAt).ThenByDescending(d => d.Id)
            .Skip(query.Page.Skip).Take(query.Page.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return (items, total);
    }

    public Task<NotificationDelivery?> FindDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken) =>
        context.Set<NotificationDelivery>().SingleOrDefaultAsync(d => d.Id == deliveryId, cancellationToken);

    public void Add(NotificationIntent intent) => context.Add(intent);

    public void Add(NotificationIntentParameter parameter) => context.Add(parameter);

    public void Add(NotificationDelivery delivery) => context.Add(delivery);

    public void Add(NotificationTemplate notificationTemplate) => context.Add(notificationTemplate);

    public void Add(NotificationPreference preference) => context.Add(preference);

    public async Task<NotificationSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        NotificationSaveOutcome outcome;
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            outcome = NotificationSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            outcome = NotificationSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation
                                                  && IsNotificationKey(violation.ConstraintName))
        {
            outcome = NotificationSaveOutcome.Duplicate;
        }

        if (outcome == NotificationSaveOutcome.Saved && _claim is not null)
        {
            await _claim.CommitAsync(cancellationToken).ConfigureAwait(false);
            await EndClaimAsync().ConfigureAwait(false);
        }
        else if (outcome != NotificationSaveOutcome.Saved)
        {
            await AbandonAsync().ConfigureAwait(false);
        }

        return outcome;
    }

    public async Task AbandonAsync()
    {
        if (_claim is not null)
        {
            await _claim.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await EndClaimAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_claim is not null)
        {
            await _claim.DisposeAsync().ConfigureAwait(false);
            _claim = null;
        }
    }

    private async Task BeginClaimAsync(CancellationToken cancellationToken)
    {
        if (_claim is not null)
        {
            throw new InvalidOperationException("A notification claim is already open; save or abandon it first.");
        }

        _claim = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A claim's rows stay with its transaction: the next claim reads its own rows fresh.</summary>
    private async Task EndClaimAsync()
    {
        await DisposeAsync().ConfigureAwait(false);
        context.ChangeTracker.Clear();
    }

    private IQueryable<InboxRow> Inbox(Guid userId) =>
        from d in context.Set<NotificationDelivery>().AsNoTracking()
        join i in context.Set<NotificationIntent>() on d.NotificationIntentId equals i.Id
        where d.RecipientUserId == userId && d.Channel == NotificationChannel.InApp
              && (d.Status == NotificationDeliveryStatus.Sent || d.Status == NotificationDeliveryStatus.Read)
        select new InboxRow { Delivery = d, Intent = i };

    private static async Task<(IReadOnlyList<(NotificationDelivery Delivery, NotificationIntent Intent)> Items, int TotalCount)> PageAsync(
        IQueryable<InboxRow> rows, int skip, int take, CancellationToken cancellationToken)
    {
        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<InboxRow> page = await rows
            .OrderByDescending(r => r.Delivery.CreatedAt).ThenByDescending(r => r.Delivery.Id)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ([.. page.Select(r => (r.Delivery, r.Intent))], total);
    }

    private static bool IsNotificationKey(string? constraint) => constraint is
        NotificationTemplateConfiguration.VersionKey or NotificationIntentConfiguration.SourceKey
        or NotificationDeliveryConfiguration.RecipientKey or NotificationPreferenceConfiguration.ChoiceKey;

    /// <summary>A delivery with its intent, as one query's row.</summary>
    private sealed class InboxRow
    {
        public required NotificationDelivery Delivery { get; init; }

        public required NotificationIntent Intent { get; init; }
    }
}
