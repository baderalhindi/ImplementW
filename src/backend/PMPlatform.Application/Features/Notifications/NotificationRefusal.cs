using PMPlatform.Application.Common.Governance;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Notifications.Contracts;

namespace PMPlatform.Application.Features.Notifications;

/// <summary>The API's answers to what WF-15 refuses.</summary>
internal static class NotificationRefusal
{
    public static AdministrationError? Of(GovernedTransitionOutcome outcome) => outcome switch
    {
        GovernedTransitionOutcome.Applied => null,
        GovernedTransitionOutcome.InvalidTransition => AdministrationError.InvalidTransition,
        GovernedTransitionOutcome.TerminalState => AdministrationError.TerminalState,
        GovernedTransitionOutcome.SeparationOfDuties => AdministrationError.Rule(NotificationErrorCodes.SeparationOfDuties),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown governed transition outcome."),
    };

    public static AdministrationError? Of(NotificationSaveOutcome outcome) => outcome switch
    {
        NotificationSaveOutcome.Saved => null,
        NotificationSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
        NotificationSaveOutcome.Duplicate => AdministrationError.Conflict(NotificationErrorCodes.TemplateVersionConflict),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown save outcome."),
    };
}
