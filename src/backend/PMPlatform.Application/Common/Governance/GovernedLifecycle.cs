using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Common.Governance;

/// <summary>
/// The governed lifecycle primitive (ERD D-12; solution architecture §11.3, M-10): DRAFT → VALIDATED → PUBLISHED →
/// RETIRED, one implementation for every authored and published row — configuration versions, master data items and
/// KPI definitions (TASK-034), permission profile versions (TASK-110). The author is the row's creator and the only
/// person who edits it while DRAFT; the reviewer who validates it and the publisher who publishes it are two other
/// people. Content is edited only while DRAFT; what a row may still change once PUBLISHED is its owner's rule.
/// </summary>
public static class GovernedLifecycle
{
    /// <summary>A DRAFT is edited by its author alone, so the reviewer reviews exactly what one other person wrote.</summary>
    public static GovernedTransitionOutcome CheckDraftEdit(GovernedEntity row, Guid editorId)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.LifecycleState switch
        {
            GovernedLifecycleState.Draft when editorId != row.CreatedBy => GovernedTransitionOutcome.SeparationOfDuties,
            GovernedLifecycleState.Draft => GovernedTransitionOutcome.Applied,
            GovernedLifecycleState.Validated or GovernedLifecycleState.Published => GovernedTransitionOutcome.InvalidTransition,
            GovernedLifecycleState.Retired => GovernedTransitionOutcome.TerminalState,
            _ => throw UnknownState(row),
        };
    }

    /// <summary>Whether <paramref name="reviewerId"/> may validate the row now: DRAFT, and not its author. Changes nothing.</summary>
    public static GovernedTransitionOutcome CheckValidate(GovernedEntity row, Guid reviewerId)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.LifecycleState switch
        {
            GovernedLifecycleState.Draft when reviewerId == row.CreatedBy => GovernedTransitionOutcome.SeparationOfDuties,
            GovernedLifecycleState.Draft => GovernedTransitionOutcome.Applied,
            GovernedLifecycleState.Validated or GovernedLifecycleState.Published => GovernedTransitionOutcome.InvalidTransition,
            GovernedLifecycleState.Retired => GovernedTransitionOutcome.TerminalState,
            _ => throw UnknownState(row),
        };
    }

    /// <summary>DRAFT → VALIDATED by a reviewer who is not the author.</summary>
    public static GovernedTransitionOutcome Validate(GovernedEntity row, Guid reviewerId, DateTimeOffset now)
    {
        GovernedTransitionOutcome outcome = CheckValidate(row, reviewerId);
        if (outcome == GovernedTransitionOutcome.Applied)
        {
            row.LifecycleState = GovernedLifecycleState.Validated;
            row.ValidatedByUserId = reviewerId;
            row.ValidatedAt = now;
        }

        return outcome;
    }

    /// <summary>Whether <paramref name="publisherId"/> may publish the row now: VALIDATED, and neither its author nor its reviewer. Changes nothing.</summary>
    public static GovernedTransitionOutcome CheckPublish(GovernedEntity row, Guid publisherId)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.LifecycleState switch
        {
            GovernedLifecycleState.Validated when publisherId == row.CreatedBy || publisherId == row.ValidatedByUserId =>
                GovernedTransitionOutcome.SeparationOfDuties,
            GovernedLifecycleState.Validated => GovernedTransitionOutcome.Applied,
            GovernedLifecycleState.Draft or GovernedLifecycleState.Published => GovernedTransitionOutcome.InvalidTransition,
            GovernedLifecycleState.Retired => GovernedTransitionOutcome.TerminalState,
            _ => throw UnknownState(row),
        };
    }

    /// <summary>VALIDATED → PUBLISHED by a publisher who is neither the author nor the reviewer.</summary>
    public static GovernedTransitionOutcome Publish(GovernedEntity row, Guid publisherId, DateTimeOffset now)
    {
        GovernedTransitionOutcome outcome = CheckPublish(row, publisherId);
        if (outcome == GovernedTransitionOutcome.Applied)
        {
            row.LifecycleState = GovernedLifecycleState.Published;
            row.PublishedByUserId = publisherId;
            row.PublishedAt = now;
        }

        return outcome;
    }

    /// <summary>Any state but RETIRED → RETIRED. An abandoned draft is retired, never deleted; RETIRED is terminal.</summary>
    public static GovernedTransitionOutcome Retire(GovernedEntity row, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.LifecycleState == GovernedLifecycleState.Retired)
        {
            return GovernedTransitionOutcome.TerminalState;
        }

        row.LifecycleState = GovernedLifecycleState.Retired;
        row.RetiredAt = now;
        return GovernedTransitionOutcome.Applied;
    }

    private static ArgumentOutOfRangeException UnknownState(GovernedEntity row) =>
        new(nameof(row), row.LifecycleState, "Unknown lifecycle state.");
}
