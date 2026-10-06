import { type ReactElement } from 'react';

import { useI18n } from '@/shared/i18n/i18n.ts';
import { StatusBadge } from '@/shared/ui/StatusBadge.tsx';

import {
  type ConcernDetail,
  type ConcernEscalationStatus,
  type ConcernStatus,
} from '../api/types.ts';
import { severityStep } from '../concernRules.ts';
import { concernStatusTone, escalationStatusTone } from '../presentation.ts';

/** The row's lifecycle: Open, Assigned, In progress, Pending validation, Resolved, Closed. */
export function ConcernStatusBadge({ status }: { status: ConcernStatus }): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge label={t(`issuesChallenges.status.${status}`)} tone={concernStatusTone(status)} />
  );
}

export function EscalationStatusBadge({
  status,
}: {
  status: ConcernEscalationStatus;
}): ReactElement {
  const { t } = useI18n();
  return (
    <StatusBadge
      label={t(`issuesChallenges.escalationStatus.${status}`)}
      tone={escalationStatusTone(status)}
    />
  );
}

/**
 * The severity the server computed, in its CONCERN_SEVERITY item's words, coloured by the overall impact level it was
 * computed from. Never an input: an unassessed concern says "Not assessed", never the lowest severity.
 */
export function SeverityBadge({
  concern,
  itemLabel,
}: {
  concern: Pick<ConcernDetail, 'severityItemId' | 'overallImpactLevel'>;
  itemLabel: (id: string) => string;
}): ReactElement {
  const { t } = useI18n();
  if (concern.severityItemId === null) {
    return <StatusBadge label={t('issuesChallenges.severity.none')} tone="neutral" />;
  }
  const step = severityStep(concern.overallImpactLevel);
  return (
    <span
      className={`badge risk-rating risk-rating--${step === null ? 'none' : String(step)}`}
      data-severity={concern.severityItemId}
    >
      {itemLabel(concern.severityItemId)}
    </span>
  );
}

/** "Escalated", with an arrow icon: the concern has an OPEN escalation. Its status does not change (ISS-GP-06). */
export function EscalatedFlag(): ReactElement {
  const { t } = useI18n();
  return (
    <span className="overdue-flag escalated-flag">
      <svg
        className="overdue-flag__icon"
        viewBox="0 0 16 16"
        width="16"
        height="16"
        aria-hidden="true"
        focusable="false"
      >
        <path d="M8 14V3M3.5 7.5 8 3l4.5 4.5" fill="none" stroke="currentColor" strokeWidth="1.8" />
      </svg>
      {t('issuesChallenges.table.escalated')}
    </span>
  );
}

/** A warning in words with the warning icon: "Overdue" or "Review due". */
export function DueFlag({ label }: { label: string }): ReactElement {
  return (
    <span className="overdue-flag">
      <svg
        className="overdue-flag__icon"
        viewBox="0 0 16 16"
        width="16"
        height="16"
        aria-hidden="true"
        focusable="false"
      >
        <path d="M8 1 15 14H1Z" fill="none" stroke="currentColor" strokeWidth="1.6" />
        <path d="M8 6v4" stroke="currentColor" strokeWidth="1.6" />
        <circle cx="8" cy="12" r="0.9" fill="currentColor" />
      </svg>
      {label}
    </span>
  );
}
