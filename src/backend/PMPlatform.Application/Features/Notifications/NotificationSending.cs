using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications;

/// <summary>
/// One attempt at one e-mail or SMS delivery, with the row locked for the attempt so no other worker sends it too. The
/// recipient is rechecked first — still a holder of a recipient role over the scope, still active, still not opted out —
/// because a retry can come hours after routing. A channel that refuses or cannot be reached leaves the delivery FAILED
/// with its next attempt after an exponential back-off, and after the last attempt DEAD_LETTER. None of this can touch
/// the source's transaction: it committed before the intent was even received.
/// </summary>
/// <remarks>
/// Delivery is at least once. If the relay accepts a message and the save that records it then fails, the next attempt
/// sends it again; a missed notification is the worse fault.
/// </remarks>
internal sealed partial class NotificationSending(
    INotificationRepository repository,
    NotificationRouting routing,
    IRoleHolderDirectory roleHolders,
    IUserContactDirectory contacts,
    IEmailSender email,
    ISmsGateway sms,
    NotificationDeliveryPolicy policy,
    TimeProvider timeProvider,
    ILogger<NotificationSending> logger)
{
    public const int ProviderMessageIdLength = 200;

    /// <summary>True if an attempt was made or the delivery was suppressed; false if it was not due or another worker holds it.</summary>
    public async Task<bool> SendAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        NotificationDelivery? delivery = await repository.ClaimDeliveryAsync(deliveryId, now, cancellationToken).ConfigureAwait(false);
        if (delivery is null)
        {
            await repository.AbandonAsync().ConfigureAwait(false);
            return false;
        }

        NotificationIntent intent = await repository.FindIntentAsync(delivery.NotificationIntentId, cancellationToken).ConfigureAwait(false)
                                    ?? throw new InvalidOperationException($"Notification delivery {delivery.Id} has no intent.");
        NotificationIntentEnvelope envelope = await repository.FindEnvelopeAsync(intent, cancellationToken).ConfigureAwait(false)
                                              ?? throw new InvalidOperationException($"The envelope of notification intent {intent.Id} is not in the outbox.");

        (RoleHolder? holder, string? ineligible) = await RecheckAsync(delivery, intent, envelope, now, cancellationToken).ConfigureAwait(false);
        string? mobileNumber = null;
        if (ineligible is null && delivery.Channel == NotificationChannel.Sms)
        {
            mobileNumber = await contacts.FindVerifiedMobileNumberAsync(delivery.RecipientUserId, cancellationToken).ConfigureAwait(false);
            ineligible = mobileNumber is null ? NotificationSuppressionReasons.MobileUnverified : null;
        }

        if (ineligible is not null)
        {
            delivery.Status = NotificationDeliveryStatus.Suppressed;
            delivery.SuppressionReason = ineligible;
            delivery.NextAttemptAt = null;
        }
        else
        {
            await AttemptAsync(delivery, holder!, mobileNumber, now, cancellationToken).ConfigureAwait(false);
        }

        delivery.UpdatedAt = now;
        delivery.UpdatedBy = NotificationServicePrincipal.Id;
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == NotificationSaveOutcome.Saved;
    }

    private async Task<(RoleHolder? Holder, string? Ineligible)> RecheckAsync(
        NotificationDelivery delivery, NotificationIntent intent, NotificationIntentEnvelope envelope, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Without the family in force there is no recipient matrix to be named by: nothing is sent on a guess.
        NotificationEventFamilyEntry? family = await routing.FindFamilyAsync(intent.EventFamilyCode, now, cancellationToken).ConfigureAwait(false);
        NotificationChannelEntry? channel = family?.Channels.SingleOrDefault(c => c.Channel == delivery.Channel);
        RoleHolder? holder = family is null || channel is null
            ? null
            : await roleHolders.FindHolderAsync(delivery.RecipientUserId, family.RecipientRoleIds, NotificationRouting.ScopeOf(envelope), cancellationToken).ConfigureAwait(false);
        if (holder is null)
        {
            return (null, NotificationSuppressionReasons.RecipientIneligible);
        }

        bool? preference = (await repository.GetPreferencesAsync([delivery.RecipientUserId], family!.Code, cancellationToken).ConfigureAwait(false))
            .SingleOrDefault(p => p.Channel == delivery.Channel)?.IsEnabled;
        return (holder, NotificationRouting.Choice(family, channel!, preference));
    }

    private async Task AttemptAsync(NotificationDelivery delivery, RoleHolder holder, string? mobileNumber, DateTimeOffset now, CancellationToken cancellationToken)
    {
        delivery.AttemptCount++;
        delivery.LastAttemptAt = now;
        try
        {
            string? providerMessageId = delivery.Channel switch
            {
                NotificationChannel.Email => await email.SendAsync(
                    new EmailMessage(holder.Email, delivery.RenderedSubject ?? string.Empty, delivery.RenderedBody, delivery.RenderedLanguage), cancellationToken).ConfigureAwait(false),
                NotificationChannel.Sms => await sms.SendAsync(mobileNumber!, delivery.RenderedBody, cancellationToken).ConfigureAwait(false),
                NotificationChannel.InApp => throw new InvalidOperationException("An in-app delivery is stored when routed, never sent."),
                _ => throw new InvalidOperationException($"Unknown notification channel {delivery.Channel}."),
            };
            delivery.Status = NotificationDeliveryStatus.Sent;
            delivery.SentAt = now;
            delivery.NextAttemptAt = null;
            delivery.FailureReason = null;
            delivery.ProviderMessageId = providerMessageId is { Length: > ProviderMessageIdLength } id ? id[..ProviderMessageIdLength] : providerMessageId;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Any fault of the channel is a failed attempt, retried or dead-lettered; it never escapes to the source.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // Only the kind of fault: a provider's message can echo an address or the content.
            delivery.FailureReason = exception.GetType().Name;
            if (delivery.AttemptCount >= policy.MaxAttempts)
            {
                delivery.Status = NotificationDeliveryStatus.DeadLetter;
                delivery.DeadLetteredAt = now;
                delivery.NextAttemptAt = null;
                LogDeadLettered(logger, delivery.Id, delivery.Channel, delivery.AttemptCount, delivery.FailureReason);
            }
            else
            {
                delivery.Status = NotificationDeliveryStatus.Failed;
                delivery.NextAttemptAt = now + (policy.RetryBaseDelay * Math.Pow(2, delivery.AttemptCount - 1));
                LogAttemptFailed(logger, delivery.Id, delivery.Channel, delivery.AttemptCount, delivery.FailureReason);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification delivery {DeliveryId} ({Channel}) attempt {Attempt} failed: {Reason}. It is tried again after its back-off.")]
    private static partial void LogAttemptFailed(ILogger logger, Guid deliveryId, NotificationChannel channel, int attempt, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Notification delivery {DeliveryId} ({Channel}) is dead-lettered after {Attempt} attempts: {Reason}.")]
    private static partial void LogDeadLettered(ILogger logger, Guid deliveryId, NotificationChannel channel, int attempt, string reason);
}
