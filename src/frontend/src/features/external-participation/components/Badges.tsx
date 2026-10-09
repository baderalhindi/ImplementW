import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import {
  type ExternalContributionStatus,
  type ExternalUpdateRequestStatus,
  type ResponseDueCondition,
  type SourceApplicationStatus,
} from '../api/types.ts';
import {
  applicationStyle,
  type ApplicationStyle,
  attemptTone,
  contributionTone,
  dueTone,
  outcomeTone,
  requestTone,
} from '../presentation.ts';
import { type ApplicationState, type ExternalOutcome, externalOutcomeOf } from '../rules.ts';

export function RequestStatusBadge({
  status,
}: {
  status: ExternalUpdateRequestStatus;
}): ReactElement {
  const { t } = useI18n();
  return (
    <span data-badge="request-status">
      <StatusBadge
        label={t(`externalParticipation.requestStatus.${status}`)}
        tone={requestTone(status)}
      />
    </span>
  );
}

/** The due date and whether the answer is due, as the server derived it; nothing at all without a due date. */
export function DueBadge({
  dueDate,
  condition,
}: {
  dueDate: string | null;
  condition: ResponseDueCondition;
}): ReactElement {
  const { t } = useI18n();
  if (dueDate === null) {
    return <span className="cell__aside">{t('externalParticipation.due.none')}</span>;
  }
  return (
    <span className="badge-group" data-badge="due">
      <span dir="ltr">{dueDate}</span>
      {condition !== 'NOT_APPLICABLE' && (
        <StatusBadge
          label={t(`externalParticipation.dueCondition.${condition}`)}
          tone={dueTone(condition)}
        />
      )}
    </span>
  );
}

/** AHDA's view of a revision: every state in its own words, the application states included. */
export function ContributionStatusBadge({
  status,
}: {
  status: ExternalContributionStatus;
}): ReactElement {
  const { t } = useI18n();
  return (
    <span data-badge="contribution-status">
      <StatusBadge
        label={t(`externalParticipation.contributionStatus.${status}`)}
        tone={contributionTone(status)}
      />
    </span>
  );
}

/** The entity's view of a revision: AHDA's application is not theirs to see, so accepted is accepted. */
export function OutcomeBadge({ status }: { status: ExternalContributionStatus }): ReactElement {
  const { t } = useI18n();
  const outcome: ExternalOutcome = externalOutcomeOf(status);
  return (
    <span data-badge="outcome">
      <StatusBadge
        label={t(`externalParticipation.outcome.${outcome}`)}
        tone={outcomeTone(outcome)}
      />
    </span>
  );
}

export function AttemptStatusBadge({ status }: { status: SourceApplicationStatus }): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge
      label={t(`externalParticipation.attemptStatus.${status}`)}
      tone={attemptTone(status)}
    />
  );
}

/** Each application style's mark: a clock, two diverging arrows, a circular arrow, a tick, a cross. */
const MARKS: Record<ApplicationStyle, string> = {
  pending: 'M8 2.5a5.5 5.5 0 1 0 0 11 5.5 5.5 0 0 0 0-11zM8 5v3.5l2.5 1.5',
  conflict: 'M2 5h9l-2-2M14 11H5l2 2M11 5l-2 2M5 11l2-2',
  retry: 'M13 8a5 5 0 1 1-1.5-3.5M13 2.5V5h-2.5',
  applied: 'M2.5 8.5l3.5 3.5 7.5-8',
  failed: 'M3.5 3.5l9 9M12.5 3.5l-9 9',
};

/**
 * Where an accepted answer's application stands (SCR-166). A conflict and a retry each have their own words, style
 * and mark, so neither reads as a plain failure (acceptance criterion 2).
 */
export function ApplicationStateBadge({ state }: { state: ApplicationState }): ReactElement {
  const { t } = useI18n();
  const style = applicationStyle(state);
  return (
    <span data-application-state={state} className={`badge badge--application-${style}`}>
      <svg
        className="badge__icon"
        viewBox="0 0 16 16"
        width="12"
        height="12"
        aria-hidden="true"
        focusable="false"
      >
        <path d={MARKS[style]} fill="none" stroke="currentColor" strokeWidth="2" />
      </svg>
      {t(`externalParticipation.applicationState.${state}`)}
    </span>
  );
}
