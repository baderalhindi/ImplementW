using PMPlatform.Application.Common.Governance;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>The API's answer to a governed lifecycle step that did not apply.</summary>
internal static class GovernedRefusal
{
    public static AdministrationError? Of(GovernedTransitionOutcome outcome) => outcome switch
    {
        GovernedTransitionOutcome.Applied => null,
        GovernedTransitionOutcome.InvalidTransition => AdministrationError.InvalidTransition,
        GovernedTransitionOutcome.TerminalState => AdministrationError.TerminalState,
        GovernedTransitionOutcome.SeparationOfDuties => AdministrationError.Rule(MasterDataConfigErrorCodes.SeparationOfDuties),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown governed transition outcome."),
    };
}
