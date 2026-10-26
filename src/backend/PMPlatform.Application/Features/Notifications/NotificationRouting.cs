using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications;

/// <summary>
/// Routes one due intent (Blueprint Section 15), in one transaction: revalidate a reminder's condition, resolve the family
/// in the NOTIFICATION_ROUTING version in force (ADR-004's three matrices), find the recipients the role matrix names
/// over the intent's scope (FG-03), and render one delivery per recipient and routed channel from the PUBLISHED templates
/// (ADR-012), each in the recipient's language. In-app is stored as sent; e-mail and SMS wait for the sending pass.
/// </summary>
/// <remarks>
/// It fails closed: an unverifiable condition, missing configuration, a missing template or a missing parameter sends
/// nothing and records why. Nothing is sent to someone the matrices do not name, or on a channel a recipient turned off.
/// </remarks>
internal sealed class NotificationRouting(
    INotificationRepository repository,
    IEnumerable<INotificationConditionSource> conditionSources,
    IConfigurationResolver configuration,
    IRoleHolderDirectory roleHolders,
    IUserContactDirectory contacts,
    IEmailSender email,
    ISmsGateway sms,
    NotificationDeliveryPolicy policy,
    TimeProvider timeProvider)
{
    public const int SubjectLength = 500;

    /// <summary>True if the intent was decided: routed, suppressed or failed. False if it was not due or another worker holds it.</summary>
    public async Task<bool> RouteAsync(Guid intentId, CancellationToken cancellationToken)
    {
        NotificationIntent? intent = await repository.ClaimIntentAsync(intentId, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (intent is null || (intent.Status == NotificationIntentStatus.Scheduled && intent.ScheduledFor > now))
        {
            await repository.AbandonAsync().ConfigureAwait(false);
            return false;
        }

        NotificationIntentEnvelope envelope = await repository.FindEnvelopeAsync(intent, cancellationToken).ConfigureAwait(false)
                                              ?? throw new InvalidOperationException($"The envelope of notification intent {intent.Id} is not in the outbox.");

        if (await RevalidateAsync(intent, envelope.Data.Condition, now, cancellationToken).ConfigureAwait(false) is { } unmet)
        {
            return await CloseAsync(intent, NotificationIntentStatus.Suppressed, unmet, now, cancellationToken).ConfigureAwait(false);
        }

        NotificationEventFamilyEntry? family = await FindFamilyAsync(intent.EventFamilyCode, now, cancellationToken).ConfigureAwait(false);
        if (family is null)
        {
            return await CloseAsync(intent, NotificationIntentStatus.Failed, NotificationSuppressionReasons.ConfigurationMissing, now, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<RoleHolder> holders = await roleHolders.FindHoldersAsync(family.RecipientRoleIds, ScopeOf(envelope), cancellationToken).ConfigureAwait(false);
        if (holders.Count == 0)
        {
            return await CloseAsync(intent, NotificationIntentStatus.Suppressed, NotificationSuppressionReasons.NoEligibleRecipient, now, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyDictionary<NotificationChannel, NotificationTemplate> templates =
            await repository.FindPublishedTemplatesAsync(intent.SourceEventType, cancellationToken).ConfigureAwait(false);
        if (family.Channels.Any(c => !templates.TryGetValue(c.Channel, out NotificationTemplate? t) || t.EventFamilyCode != intent.EventFamilyCode))
        {
            return await CloseAsync(intent, NotificationIntentStatus.Failed, NotificationSuppressionReasons.TemplateMissing, now, cancellationToken).ConfigureAwait(false);
        }

        Dictionary<string, string> parameters = (await repository.GetParametersAsync(intent.Id, cancellationToken).ConfigureAwait(false))
            .ToDictionary(p => p.ParameterKey, p => p.ParameterValue, StringComparer.Ordinal);
        Dictionary<(Guid, NotificationChannel), bool> preferences = (await repository
                .GetPreferencesAsync([.. holders.Select(h => h.UserId)], family.Code, cancellationToken).ConfigureAwait(false))
            .ToDictionary(p => (p.UserId, p.Channel), p => p.IsEnabled);

        List<NotificationDelivery> deliveries = [];
        foreach (RoleHolder holder in holders)
        {
            foreach (NotificationChannelEntry channel in family.Channels)
            {
                NotificationTemplate template = templates[channel.Channel];
                RenderedNotification? rendered = Render(template, holder, parameters, intent.DeepLink!, channel.Channel);
                if (rendered is null)
                {
                    return await CloseAsync(intent, NotificationIntentStatus.Failed, NotificationSuppressionReasons.TemplateParameterMissing, now, cancellationToken).ConfigureAwait(false);
                }

                bool? preference = preferences.TryGetValue((holder.UserId, channel.Channel), out bool enabled) ? enabled : null;
                string? suppression = await DecideAsync(holder, family, channel, preference, rendered, cancellationToken).ConfigureAwait(false);
                deliveries.Add(NewDelivery(intent, holder, channel.Channel, template, rendered, suppression, now));
            }
        }

        deliveries.ForEach(repository.Add);
        intent.Status = deliveries.Any(d => d.Status == NotificationDeliveryStatus.Pending) ? NotificationIntentStatus.Routed : NotificationIntentStatus.Completed;
        Touch(intent, now);
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == NotificationSaveOutcome.Saved;
    }

    /// <summary>The family in the NOTIFICATION_ROUTING version in force now; null when there is none or it lacks the family.</summary>
    public async Task<NotificationEventFamilyEntry?> FindFamilyAsync(string familyCode, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            return (await configuration.ResolveAsync(ConfigurationFamilyCodes.NotificationRouting, now, cancellationToken).ConfigureAwait(false))
                .RequireNotificationEventFamily(familyCode);
        }
        catch (ConfigurationMissingException)
        {
            return null;
        }
    }

    public static RoleHolderScope ScopeOf(NotificationIntentEnvelope envelope) =>
        new(envelope.Scope.ProjectId, envelope.Scope.DepartmentId, envelope.Scope.ExternalEntityId);

    /// <summary>
    /// Whether the family reaches this person on this channel now. In-app is always on; a mandatory family keeps the
    /// channel's default and cannot be turned off; otherwise the person's choice, or the channel's default without one.
    /// </summary>
    public static string? Choice(NotificationEventFamilyEntry family, NotificationChannelEntry channel, bool? preference) =>
        channel.Channel == NotificationChannel.InApp ? null
        : family.IsMandatory || preference is null ? (channel.EnabledByDefault ? null : NotificationSuppressionReasons.ChannelOffByDefault)
        : preference.Value ? null : NotificationSuppressionReasons.RecipientOptedOut;

    private async Task<string?> RevalidateAsync(NotificationIntent intent, NotificationCondition? condition, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (condition is null)
        {
            return null;
        }

        INotificationConditionSource? source = conditionSources.Where(s => s.SourceModule == intent.SourceModule).ToList() switch
        {
            [] => null,
            [var one] => one,
            _ => throw new InvalidOperationException($"More than one notification condition source is registered for {intent.SourceModule}."),
        };
        if (source is null)
        {
            return NotificationSuppressionReasons.ConditionUnverifiable;
        }

        string? status = await source.FindStatusAsync(condition.SubjectType, condition.SubjectId, cancellationToken).ConfigureAwait(false);
        intent.ConditionRevalidatedAt = now;
        return status is not null && condition.SatisfiedWhenStatusIn.Contains(status, StringComparer.Ordinal) ? null : NotificationSuppressionReasons.ConditionResolved;
    }

    private async Task<string?> DecideAsync(
        RoleHolder holder, NotificationEventFamilyEntry family, NotificationChannelEntry channel, bool? preference, RenderedNotification rendered, CancellationToken cancellationToken)
    {
        if (Choice(family, channel, preference) is { } off)
        {
            return off;
        }

        switch (channel.Channel)
        {
            case NotificationChannel.InApp:
                return null;
            case NotificationChannel.Email:
                return email.IsConfigured && policy.AppBaseUrl is not null ? null : NotificationSuppressionReasons.ChannelNotConfigured;
            case NotificationChannel.Sms:
                if (!sms.IsConfigured || policy.AppBaseUrl is null)
                {
                    return NotificationSuppressionReasons.ChannelNotConfigured;
                }

                if (await contacts.FindVerifiedMobileNumberAsync(holder.UserId, cancellationToken).ConfigureAwait(false) is null)
                {
                    return NotificationSuppressionReasons.MobileUnverified;
                }

                return rendered.SegmentCount > policy.SmsMaxSegments ? NotificationSuppressionReasons.SmsSegmentBudgetExceeded : null;
            default:
                throw new InvalidOperationException($"Unknown notification channel {channel.Channel}.");
        }
    }

    /// <summary>In the recipient's language; e-mail and SMS link to the absolute URL, in-app to the route.</summary>
    private RenderedNotification? Render(
        NotificationTemplate template, RoleHolder holder, Dictionary<string, string> parameters, string deepLink, NotificationChannel channel)
    {
        Dictionary<string, string> values = new(parameters, StringComparer.Ordinal)
        {
            [TemplateText.DeepLink] = channel != NotificationChannel.InApp && policy.AppBaseUrl is not null ? policy.AbsoluteLink(deepLink) : deepLink,
        };
        bool arabic = holder.PreferredLanguage == Domain.Common.Language.Ar;
        string? subjectText = arabic ? template.SubjectAr : template.SubjectEn;
        string? subject = subjectText is null ? null : TemplateText.Render(subjectText, values);
        string? body = TemplateText.Render(arabic ? template.BodyAr : template.BodyEn, values);
        if (body is null || (subjectText is not null && subject is null))
        {
            return null;
        }

        // A subject is one header line: a parameter cannot add another.
        subject = subject?.ReplaceLineEndings(" ");
        return new RenderedNotification(
            subject is { Length: > SubjectLength } ? subject[..SubjectLength] : subject,
            body,
            channel == NotificationChannel.Sms ? (short)SmsSegments.Count(body) : null);
    }

    private static NotificationDelivery NewDelivery(
        NotificationIntent intent, RoleHolder holder, NotificationChannel channel, NotificationTemplate template, RenderedNotification rendered, string? suppression, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            NotificationIntentId = intent.Id,
            RecipientUserId = holder.UserId,
            Channel = channel,
            NotificationTemplateId = template.Id,
            RenderedLanguage = holder.PreferredLanguage,
            RenderedSubject = rendered.Subject,
            RenderedBody = rendered.Body,
            SegmentCount = rendered.SegmentCount,
            Status = suppression is not null ? NotificationDeliveryStatus.Suppressed
                : channel == NotificationChannel.InApp ? NotificationDeliveryStatus.Sent
                : NotificationDeliveryStatus.Pending,
            SuppressionReason = suppression,
            SentAt = suppression is null && channel == NotificationChannel.InApp ? now : null,
            NextAttemptAt = suppression is null && channel != NotificationChannel.InApp ? now : null,
            CreatedAt = now,
            CreatedBy = NotificationServicePrincipal.Id,
            UpdatedAt = now,
            UpdatedBy = NotificationServicePrincipal.Id,
        };

    private async Task<bool> CloseAsync(NotificationIntent intent, NotificationIntentStatus status, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        intent.Status = status;
        intent.SuppressionReason = reason;
        Touch(intent, now);
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == NotificationSaveOutcome.Saved;
    }

    private static void Touch(NotificationIntent intent, DateTimeOffset now)
    {
        intent.UpdatedAt = now;
        intent.UpdatedBy = NotificationServicePrincipal.Id;
    }

    private sealed record RenderedNotification(string? Subject, string Body, short? SegmentCount);
}
